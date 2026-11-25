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
    private readonly Dictionary<int, FunctionId> _mapped = [];
    private readonly Dictionary<int, GlobalId> _globals = [];
    private readonly Queue<FunctionId> _pending = new();

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
        _destination.CImports.AddRange(_source.CImports);
        foreach (FunctionId root in roots)
        {
            Reach(root);
        }

        while (_pending.Count > 0)
        {
            FunctionId id = _pending.Dequeue();
            Body? body = _provider.Lower(id);
            FunctionDecl declaration = _source.Functions[id.Index];
            if (body is null)
            {
                if (declaration.Linkage == LinkageKind.Internal)
                {
                    ReportMissingBody(declaration);
                }

                continue;
            }

            _destination.Bodies[_mapped[id.Index].Index] = CopyBody(body);
        }

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

    private FunctionId Reach(FunctionId id)
    {
        if (_mapped.TryGetValue(id.Index, out FunctionId existing))
        {
            return existing;
        }

        FunctionDecl declaration = _source.Functions[id.Index];
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

        FunctionId created = _builder.DeclareFunction(
            _source.Text(declaration.Key),
            _source.Text(declaration.Name),
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
        _mapped.Add(id.Index, created);
        _pending.Enqueue(id);
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
                OperandKind.FnRef => operand with { A = Reach(new FunctionId(operand.A)).Index },
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
                copy = copy with { CalleeId = Reach(new FunctionId(rvalue.CalleeId)).Index };
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
