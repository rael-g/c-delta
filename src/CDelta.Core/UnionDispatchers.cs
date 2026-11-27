using CDelta.Ir;

namespace CDelta.Core;

public sealed partial class Instantiator
{
    private readonly Dictionary<string, FunctionId> _dispatchers = [];

    private FunctionId UnionDispatcher(Body source, Rvalue rvalue)
    {
        TypeId pointerType = IrTyper.OfOperand(_source, source, new OperandId(rvalue.CalleeId));
        TypeId unionSource = new(_source.Types[pointerType.Index].A);
        UnionDecl declaration = _source.Unions[_source.Types[unionSource.Index].A];
        TypeId destinationPointer = _remap.Type(pointerType);
        TypeId unionType = _remap.Type(unionSource);
        TypeId contract = _remap.Type(declaration.Interface);
        string key = "union:" + IrPrinter.TypeText(_destination, unionType) + ":" + rvalue.Aux;
        if (_dispatchers.TryGetValue(key, out FunctionId existing))
        {
            return existing;
        }

        (List<TypeId> parameters, TypeId result, InterfaceMethod method) = IrTypes.InterfaceMethodSignature(_destination, contract, rvalue.Aux)!.Value;
        var declared = new List<ParamDecl> { new(_destination.Intern("this"), destinationPointer, ParamMode.Value) };
        for (int i = 0; i < parameters.Count; i++)
        {
            declared.Add(new ParamDecl(_destination.Intern("a" + i), parameters[i], ParamMode.Value));
        }

        FunctionId id = _builder.DeclareFunction(key, key, declaration.Span, Origin.Synthetic, declared, false, result, CallConv.Default, LinkageKind.Internal, null, []);
        _dispatchers.Add(key, id);

        string methodName = _destination.Text(method.Name);
        var body = new Body();
        var builder = new FunctionBuilder(_destination, body);
        LocalId self = builder.NewLocal("this", destinationPointer, LocalKind.Param, SourceSpan.None);
        var arguments = new List<LocalId>();
        for (int i = 0; i < parameters.Count; i++)
        {
            arguments.Add(builder.NewLocal("a" + i, parameters[i], LocalKind.Param, SourceSpan.None));
        }

        TypeId tagType = _builder.Primitive(TypeKind.U32);
        LocalId tag = builder.NewLocal(null, tagType, LocalKind.Temp, SourceSpan.None);
        BlockId entry = builder.NewBlock();
        int members = declaration.Members.Count;
        var blocks = new List<BlockId>();
        for (int k = 0; k < members; k++)
        {
            blocks.Add(builder.NewBlock());
        }

        BlockId fallback = builder.NewBlock();
        builder.SetBlock(entry);
        builder.Assign(builder.LocalPlace(tag), builder.Tag(builder.DerefPlace(self)), SourceSpan.None);
        var cases = new List<SwitchCase>();
        for (int k = 0; k < members; k++)
        {
            cases.Add(new SwitchCase(_builder.IntConstant(tagType, k), blocks[k]));
        }

        builder.Switch(builder.Copy(builder.LocalPlace(tag)), cases, fallback, SourceSpan.None);
        builder.SetBlock(fallback);
        builder.Unreachable(UnreachableMode.Default, SourceSpan.None);
        TypeNode interfaceNode = _source.Types[declaration.Interface.Index];
        bool returnsValue = _destination.Types[result.Index].Kind != TypeKind.Void;
        for (int k = 0; k < members; k++)
        {
            TypeId memberSource = _source.UnionMembers[declaration.Members.Start + k];
            TypeId memberType = _remap.Type(memberSource);
            ImplDecl impl = FindImpl(memberType, interfaceNode, out Dictionary<int, TypeId> bindings, out _);
            FunctionId target = ReachImplMethod(impl, bindings, methodName);
            ParamDecl thisParameter = _destination.Params[_destination.Functions[target.Index].Params.Start];
            bool mutable = !_destination.Types[thisParameter.Type.Index].Flags.HasFlag(TypeFlags.Const);
            builder.SetBlock(blocks[k]);
            LocalId address = builder.NewLocal(null, thisParameter.Type, LocalKind.Temp, SourceSpan.None);
            builder.Assign(
                builder.LocalPlace(address),
                builder.Addr(builder.Project(builder.DerefPlace(self), ProjectionKind.Payload, k), mutable),
                SourceSpan.None);
            var callArguments = new List<OperandId> { builder.Copy(builder.LocalPlace(address)) };
            callArguments.AddRange(arguments.Select(argument => builder.Copy(builder.LocalPlace(argument))));
            RvalueId call = builder.CallDirect(target, callArguments);
            if (returnsValue)
            {
                LocalId value = builder.NewLocal(null, result, LocalKind.Temp, SourceSpan.None);
                builder.Assign(builder.LocalPlace(value), call, SourceSpan.None);
                builder.Return(builder.Copy(builder.LocalPlace(value)), SourceSpan.None);
            }
            else
            {
                builder.CallStatement(call, SourceSpan.None);
                builder.Return(OperandId.None, SourceSpan.None);
            }
        }

        _destination.Bodies[id.Index] = body;
        return id;
    }

    private FunctionId ReachImplMethod(ImplDecl impl, Dictionary<int, TypeId> bindings, string methodName)
    {
        for (int i = 0; i < impl.Methods.Count; i++)
        {
            ImplMethod item = _source.ImplMethods[impl.Methods.Start + i];
            if (_source.Text(item.Name) != methodName)
            {
                continue;
            }

            Dictionary<int, TypeId>? saved = _remap.Bindings;
            _remap.Bindings = bindings;
            List<TypeId> arguments = item.TypeArgs.IsNone
                ? []
                : _source.TypeLists[item.TypeArgs.Index].Select(argument => _remap.Type(new TypeId(argument))).ToList();
            _remap.Bindings = saved;
            return Reach(item.Function, arguments);
        }

        throw new InvalidOperationException("an impl has no method " + methodName);
    }
}
