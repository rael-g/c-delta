using CDelta.Diagnostics;
using CDelta.Ir;

namespace CDelta.Core;

public sealed class Instantiator
{
    private readonly Module _source;
    private readonly IBodyProvider _provider;
    private readonly DiagnosticBag _diagnostics;
    private readonly Module _destination;
    private readonly ModuleBuilder _builder;
    private readonly ModuleRemapper _remap;
    private const int MaxInstantiationDepth = 64;

    private readonly Dictionary<string, FunctionId> _mapped = [];
    private readonly Dictionary<int, Body?> _bodies = [];
    private readonly Dictionary<int, GlobalId> _globals = [];
    private readonly Queue<Instance> _pending = new();
    private int _depth;

    private readonly record struct Instance(FunctionId Source, FunctionId Destination, Dictionary<int, TypeId> Bindings, int Depth);

    public Instantiator(Module source, IBodyProvider provider, DiagnosticBag diagnostics)
    {
        _source = source;
        _provider = provider;
        _diagnostics = diagnostics;
        _destination = new Module(source.Name) { Stage = ModuleStage.Instantiated };
        _builder = new ModuleBuilder(_destination);
        _remap = new ModuleRemapper(source, _destination);
    }

    public Module Run(IReadOnlyList<FunctionId> roots)
    {
        _destination.Sources.AddRange(_source.Sources);
        foreach (FunctionId root in roots)
        {
            if (_source.Functions[root.Index].TypeParams.Count == 0)
            {
                Reach(root, []);
            }
        }

        while (_pending.Count > 0)
        {
            Instance instance = _pending.Dequeue();
            FunctionId id = instance.Source;
            if (!_bodies.TryGetValue(id.Index, out Body? body))
            {
                body = _provider.Lower(id);
                _bodies.Add(id.Index, body);
            }

            FunctionDecl declaration = _source.Functions[id.Index];
            if (body is null)
            {
                if (declaration.Linkage == LinkageKind.Internal)
                {
                    ReportMissingBody(declaration);
                }

                continue;
            }

            _remap.Bindings = instance.Bindings;
            _depth = instance.Depth;
            _destination.Bodies[instance.Destination.Index] = CopyBody(body);
            _remap.Bindings = null;
        }

        _destination.CImports.AddRange(_source.CImports);
        return _destination;
    }

    private void ReportMissingBody(FunctionDecl declaration)
    {
        string path = declaration.Span.IsNone ? _source.Name : _source.Sources[declaration.Span.File.Index].FullPath;
        if (!_diagnostics.HasError)
        {
            _diagnostics.Add(
                DiagnosticCode.CD9001,
                new Location(path, declaration.Span.Line, declaration.Span.Column),
                new Note(null, "no body was produced for " + _source.Text(declaration.Key)));
        }
    }

    private FunctionId Reach(FunctionId id, IReadOnlyList<TypeId> typeArguments)
    {
        string instanceKey = id.Index + "|" + string.Join(",", typeArguments.Select(argument => argument.Index));
        if (_mapped.TryGetValue(instanceKey, out FunctionId existing))
        {
            return existing;
        }

        FunctionDecl declaration = _source.Functions[id.Index];
        int depth = typeArguments.Count > 0 ? _depth + 1 : _depth;
        if (depth > MaxInstantiationDepth)
        {
            if (!_diagnostics.HasError)
            {
                string path = declaration.Span.IsNone ? _source.Name : _source.Sources[declaration.Span.File.Index].FullPath;
                _diagnostics.Add(
                    DiagnosticCode.CD5005,
                    new Location(path, declaration.Span.Line, declaration.Span.Column),
                    new Note(null, "while instantiating " + _source.Text(declaration.Key)));
            }

            return existing;
        }

        var bindings = new Dictionary<int, TypeId>();
        for (int i = 0; i < declaration.TypeParams.Count; i++)
        {
            bindings[_source.TypeParams[declaration.TypeParams.Start + i].Index] = typeArguments[i];
        }

        Dictionary<int, TypeId>? saved = _remap.Bindings;
        _remap.Bindings = bindings;
        var parameters = new List<ParamDecl>();
        for (int i = 0; i < declaration.Params.Count; i++)
        {
            ParamDecl parameter = _source.Params[declaration.Params.Start + i];
            parameters.Add(new ParamDecl(_remap.String(parameter.Name), _remap.Type(parameter.Type), parameter.Mode));
        }

        var attributes = new List<string>();
        for (int i = 0; i < declaration.Attributes.Count; i++)
        {
            attributes.Add(_source.Text(_source.Attributes[declaration.Attributes.Start + i].Name));
        }

        string instanceName = typeArguments.Count == 0
            ? _source.Text(declaration.Key)
            : _source.Text(declaration.Key) + "<" + string.Join(", ", typeArguments.Select(argument => IrPrinter.TypeText(_destination, argument))) + ">";
        FunctionId created = _builder.DeclareFunction(
            instanceName,
            typeArguments.Count == 0 ? _source.Text(declaration.Name) : instanceName,
            declaration.Span,
            _remap.Origin(declaration.Origin),
            parameters,
            declaration.Variadic,
            _remap.Type(declaration.Return),
            declaration.Conv,
            declaration.Linkage,
            declaration.ExportName.IsNone ? null : _source.Text(declaration.ExportName),
            attributes,
            declaration.ReturnMode);
        _destination.Functions[created.Index] = _destination.Functions[created.Index] with
        {
            Inline = declaration.Inline,
            Safety = declaration.Safety,
            ExportWeak = declaration.ExportWeak,
        };
        _remap.Bindings = saved;
        _mapped.Add(instanceKey, created);
        _pending.Enqueue(new Instance(id, created, bindings, depth));
        return created;
    }

    private GlobalId ReachGlobal(GlobalId id)
    {
        if (_globals.TryGetValue(id.Index, out GlobalId existing))
        {
            return existing;
        }

        GlobalDecl declaration = _source.Globals[id.Index];
        GlobalId created = _builder.DeclareGlobal(
            _source.Text(declaration.Key),
            _source.Text(declaration.Name),
            declaration.Span,
            _remap.Origin(declaration.Origin),
            _remap.Type(declaration.Type),
            declaration.Mutable,
            declaration.ThreadLocal,
            declaration.Initializer.IsNone ? ConstId.None : _remap.Constant(declaration.Initializer),
            declaration.Linkage,
            declaration.ExportName.IsNone ? null : _source.Text(declaration.ExportName));
        _destination.Globals[created.Index] = _destination.Globals[created.Index] with
        {
            Section = _remap.String(declaration.Section),
            Alignment = declaration.Alignment,
        };
        _globals.Add(id.Index, created);
        return created;
    }

    private Body CopyBody(Body source)
    {
        var body = new Body();
        foreach (LocalDecl local in source.Locals)
        {
            body.Locals.Add(local with { Name = _remap.String(local.Name), Type = _remap.Type(local.Type) });
        }

        foreach (Block block in source.Blocks)
        {
            body.Blocks.Add(new Block(new List<StatementId>(block.Statements), block.Terminator));
        }

        foreach (Statement statement in source.Statements)
        {
            body.Statements.Add(statement with { Message = _remap.String(statement.Message) });
        }

        foreach (Place place in source.Places)
        {
            body.Places.Add(place.Base == PlaceBase.Global ? place with { Id = ReachGlobal(new GlobalId(place.Id)).Index } : place);
        }

        body.Projections.AddRange(source.Projections);
        foreach (Operand operand in source.Operands)
        {
            body.Operands.Add(operand.Kind switch
            {
                OperandKind.Const => operand with { A = _remap.Constant(new ConstId(operand.A)).Index },
                OperandKind.FnRef => operand with { A = Reach(new FunctionId(operand.A), []).Index },
                OperandKind.Undef => operand with { A = _remap.Type(new TypeId(operand.A)).Index },
                _ => operand,
            });
        }

        foreach (Rvalue rvalue in source.Rvalues)
        {
            Rvalue copy = rvalue with { Type = _remap.Type(rvalue.Type) };
            if (rvalue.Kind == RvalueKind.Intrinsic)
            {
                copy = copy with { Aux = _remap.String(new StringId(rvalue.Aux)).Index };
            }

            if (rvalue.Kind == RvalueKind.Call && rvalue.Callee == CalleeKind.Direct)
            {
                int[] typeArgs = IrTypes.CallTypeArgs(_source, rvalue);
                var mappedArgs = typeArgs.Select(argument => _remap.Type(new TypeId(argument))).ToList();
                copy = copy with { CalleeId = Reach(new FunctionId(rvalue.CalleeId), mappedArgs).Index, Aux = 0 };
            }

            body.Rvalues.Add(copy);
        }

        body.Args.AddRange(source.Args);
        foreach (SwitchCase item in source.SwitchCases)
        {
            body.SwitchCases.Add(item with { Value = _remap.Constant(item.Value) });
        }

        return body;
    }
}
