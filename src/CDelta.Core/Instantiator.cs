using CDelta.Diagnostics;
using CDelta.Ir;

namespace CDelta.Core;

public sealed partial class Instantiator
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
    private Dictionary<string, ImplDecl>? _impls;
    private readonly List<ImplDecl> _genericImpls = [];
    private readonly Dictionary<string, int> _reachedImpls = [];

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
            _destination.Bodies[instance.Destination.Index] = CopyBody(body, IsDefaultFunction(instance.Source));
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

    private Body CopyBody(Body source, bool insideDefault)
    {
        var defaultCalls = new List<int>();
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

        var unionCalls = new List<int>();
        foreach (Rvalue rvalue in source.Rvalues)
        {
            Rvalue copy = rvalue with { Type = _remap.Type(rvalue.Type) };
            if (rvalue.Kind == RvalueKind.Call && rvalue.Callee == CalleeKind.Union)
            {
                unionCalls.Add(body.Rvalues.Count);
                copy = copy with { Callee = CalleeKind.Direct, CalleeId = UnionDispatcher(source, rvalue).Index, Aux = 0 };
            }

            if (rvalue.Kind == RvalueKind.Intrinsic)
            {
                copy = copy with { Aux = _remap.String(new StringId(rvalue.Aux)).Index };
            }

            if (rvalue.Kind == RvalueKind.ClosureMake)
            {
                copy = copy with { CalleeId = Reach(new FunctionId(rvalue.CalleeId), []).Index };
            }

            if (rvalue.Kind == RvalueKind.DynMake)
            {
                copy = copy with { Callee = CalleeKind.Direct, Aux = ReachImpl(rvalue) };
            }

            if (rvalue.Kind == RvalueKind.Call && rvalue.Callee == CalleeKind.Constrained)
            {
                copy = ResolveConstrained(rvalue, copy);
                if (_resolvedToDefault)
                {
                    defaultCalls.Add(body.Rvalues.Count);
                }
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
        foreach (int index in unionCalls)
        {
            Rvalue call = body.Rvalues[index];
            Rvalue original = source.Rvalues[index];
            int start = body.Args.Count;
            body.Args.Add(new OperandId(original.CalleeId));
            for (int i = 0; i < original.Args.Count; i++)
            {
                body.Args.Add(source.Args[original.Args.Start + i]);
            }

            body.Rvalues[index] = call with { Args = new IdRange(start, original.Args.Count + 1) };
        }

        foreach (SwitchCase item in source.SwitchCases)
        {
            body.SwitchCases.Add(item with { Value = _remap.Constant(item.Value) });
        }

        CopyDefaultReceivers(body, defaultCalls, insideDefault);
        TypeTestFolding.Fold(_destination, body);
        return body;
    }

    private ImplDecl FindImpl(TypeId concrete, TypeNode interfaceNode, out Dictionary<int, TypeId> bindings, out string key)
    {
        key = ImplKey(concrete, interfaceNode);
        _impls ??= IndexImpls();
        bindings = [];
        if (!_impls.TryGetValue(key, out ImplDecl impl) && !TryMatchGeneric(concrete, interfaceNode, out impl, out bindings))
        {
            throw new InvalidOperationException("no impl of " + key + " was found");
        }

        return impl;
    }

    private int ReachImpl(Rvalue rvalue)
    {
        ImplDecl impl;
        Dictionary<int, TypeId> bindings;
        string key;
        if (rvalue.Callee == CalleeKind.Constrained)
        {
            ConstrainedRef reference = _source.ConstrainedRefs[rvalue.Aux];
            impl = FindImpl(_remap.Bindings![reference.Param.Index], _source.Types[reference.Interface.Index], out bindings, out key);
        }
        else
        {
            impl = _source.Impls[rvalue.Aux];
            bindings = [];
            _impls ??= IndexImpls();
            key = _impls.First(pair => pair.Value.Key == impl.Key).Key;
        }

        if (_reachedImpls.TryGetValue(key, out int existing))
        {
            return existing;
        }

        Dictionary<int, TypeId>? saved = _remap.Bindings;
        _remap.Bindings = bindings;
        TypeId type = _remap.Type(impl.Type);
        TypeId contract = _remap.Type(impl.Interface);
        var methods = new List<ImplMethod>();
        for (int i = 0; i < impl.Methods.Count; i++)
        {
            ImplMethod item = _source.ImplMethods[impl.Methods.Start + i];
            List<TypeId> arguments = item.TypeArgs.IsNone
                ? []
                : _source.TypeLists[item.TypeArgs.Index].Select(argument => _remap.Type(new TypeId(argument))).ToList();
            methods.Add(new ImplMethod(_remap.String(item.Name), Reach(item.Function, arguments), TypeListId.None));
        }

        _remap.Bindings = saved;
        _builder.DeclareImpl(key, type, contract, methods);
        _reachedImpls.Add(key, _destination.Impls.Count - 1);
        return _destination.Impls.Count - 1;
    }

    private Rvalue ResolveConstrained(Rvalue rvalue, Rvalue copy)
    {
        ConstrainedRef reference = _source.ConstrainedRefs[rvalue.CalleeId];
        TypeId concrete = _remap.Bindings![reference.Param.Index];
        TypeNode interfaceNode = _source.Types[reference.Interface.Index];
        ImplDecl impl = FindImpl(concrete, interfaceNode, out Dictionary<int, TypeId> bindings, out string key);

        string method = _source.Text(reference.Method);
        for (int i = 0; i < impl.Methods.Count; i++)
        {
            ImplMethod item = _source.ImplMethods[impl.Methods.Start + i];
            if (_source.Text(item.Name) != method)
            {
                continue;
            }

            var arguments = new List<TypeId>();
            Dictionary<int, TypeId>? saved = _remap.Bindings;
            _remap.Bindings = bindings;
            if (!item.TypeArgs.IsNone)
            {
                arguments.AddRange(_source.TypeLists[item.TypeArgs.Index].Select(argument => _remap.Type(new TypeId(argument))));
            }

            _remap.Bindings = saved;
            arguments.AddRange(IrTypes.CallTypeArgs(_source, rvalue).Select(argument => _remap.Type(new TypeId(argument))));
            _resolvedToDefault = IsDefaultFunction(item.Function);
            return copy with { Callee = CalleeKind.Direct, CalleeId = Reach(item.Function, arguments).Index, Aux = 0 };
        }

        throw new InvalidOperationException("the impl " + key + " has no method " + method);
    }

    private Dictionary<string, ImplDecl> IndexImpls()
    {
        var impls = new Dictionary<string, ImplDecl>();
        Dictionary<int, TypeId>? saved = _remap.Bindings;
        _remap.Bindings = null;
        foreach (ImplDecl impl in _source.Impls)
        {
            if (HasParam(impl.Type))
            {
                _genericImpls.Add(impl);
                continue;
            }

            impls[ImplKey(_remap.Type(impl.Type), _source.Types[impl.Interface.Index])] = impl;
        }

        _remap.Bindings = saved;
        return impls;
    }

    private bool HasParam(TypeId type)
    {
        TypeNode node = _source.Types[type.Index];
        return node.Kind switch
        {
            TypeKind.Param => true,
            TypeKind.Struct or TypeKind.Interface => IrTypes.StructTypeArgs(_source, node).Any(argument => HasParam(new TypeId(argument))),
            _ => false,
        };
    }

    private bool TryMatchGeneric(TypeId concrete, TypeNode interfaceNode, out ImplDecl found, out Dictionary<int, TypeId> bindings)
    {
        found = default;
        bindings = [];
        TypeNode destination = _destination.Types[concrete.Index];
        if (destination.Kind != TypeKind.Struct || !_remap.Instances.TryGetValue(new StructId(destination.A), out (StructId Source, IReadOnlyList<TypeId> Arguments) origin))
        {
            return false;
        }

        string wanted = ImplKey(concrete, interfaceNode);
        foreach (ImplDecl impl in _genericImpls)
        {
            TypeNode implNode = _source.Types[impl.Type.Index];
            TypeNode implInterface = _source.Types[impl.Interface.Index];
            if (implNode.Kind != TypeKind.Struct || implNode.A != origin.Source.Index || implInterface.A != interfaceNode.A)
            {
                continue;
            }

            var candidate = new Dictionary<int, TypeId>();
            int[] implArguments = IrTypes.StructTypeArgs(_source, implNode);
            if (!implArguments.Select((argument, index) => Unify(new TypeId(argument), origin.Arguments[index], candidate)).All(matched => matched))
            {
                continue;
            }

            Dictionary<int, TypeId>? saved = _remap.Bindings;
            _remap.Bindings = candidate;
            string actual = ImplKey(concrete, implInterface);
            _remap.Bindings = saved;
            if (actual == wanted)
            {
                found = impl;
                bindings = candidate;
                return true;
            }
        }

        return false;
    }

    private bool Unify(TypeId pattern, TypeId concrete, Dictionary<int, TypeId> bindings)
    {
        TypeNode node = _source.Types[pattern.Index];
        if (node.Kind == TypeKind.Param)
        {
            return bindings.TryGetValue(node.A, out TypeId existing) ? existing == concrete : bindings.TryAdd(node.A, concrete);
        }

        if (HasParam(pattern))
        {
            return false;
        }

        Dictionary<int, TypeId>? saved = _remap.Bindings;
        _remap.Bindings = null;
        bool same = _remap.Type(pattern) == concrete;
        _remap.Bindings = saved;
        return same;
    }

    private string ImplKey(TypeId type, TypeNode interfaceNode)
    {
        IEnumerable<string> arguments = IrTypes.StructTypeArgs(_source, interfaceNode)
            .Select(argument => IrPrinter.TypeText(_destination, _remap.Type(new TypeId(argument))));
        return IrPrinter.TypeText(_destination, type) + " : " + _source.Text(_source.Interfaces[interfaceNode.A].Key)
            + "<" + string.Join(", ", arguments) + ">";
    }
}
