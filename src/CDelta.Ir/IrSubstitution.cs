namespace CDelta.Ir;

public static class IrSubstitution
{
    public static TypeId Apply(Module module, TypeId id, IReadOnlyDictionary<int, TypeId> bindings)
    {
        if (id.IsNone || bindings.Count == 0)
        {
            return id;
        }

        TypeNode node = module.Types[id.Index];
        switch (node.Kind)
        {
            case TypeKind.Param:
                return bindings.TryGetValue(node.A, out TypeId bound) ? bound : id;
            case TypeKind.Ptr:
            case TypeKind.Slice:
            case TypeKind.Optional:
            case TypeKind.Array:
                return module.Type(node with { A = Apply(module, new TypeId(node.A), bindings).Index });
            case TypeKind.Result:
                return module.Type(node with
                {
                    A = Apply(module, new TypeId(node.A), bindings).Index,
                    B = Apply(module, new TypeId(node.B), bindings).Index,
                });
            case TypeKind.Struct or TypeKind.Interface when node.B > 0:
                int[] arguments = module.TypeLists[node.B - 1];
                int[] substituted = new int[arguments.Length];
                for (int i = 0; i < arguments.Length; i++)
                {
                    substituted[i] = Apply(module, new TypeId(arguments[i]), bindings).Index;
                }

                return module.Type(node with { B = module.TypeList(substituted).Index + 1 });
            case TypeKind.Fn:
                SigNode sig = module.Sigs[node.A];
                int[] items = module.TypeLists[sig.Params.Index];
                int[] mapped = new int[items.Length];
                for (int i = 0; i < items.Length; i++)
                {
                    var parameter = SigParam.Decode(items[i]);
                    mapped[i] = new SigParam(Apply(module, parameter.Type, bindings), parameter.Mode).Encode();
                }

                SigId created = module.Sig(sig with { Params = module.TypeList(mapped), Return = Apply(module, sig.Return, bindings) });
                return module.Type(node with { A = created.Index });
            default:
                return id;
        }
    }

    public static TypeId FieldType(Module module, TypeNode structNode, int field)
    {
        StructDecl declaration = module.Structs[structNode.A];
        TypeId type = module.Fields[declaration.Fields.Start + field].Type;
        int[] arguments = IrTypes.StructTypeArgs(module, structNode);
        return arguments.Length == 0 ? type : Apply(module, type, Bind(module, declaration.TypeParams, arguments));
    }

    public static Dictionary<int, TypeId> Bind(Module module, FunctionDecl function, IReadOnlyList<int> arguments) =>
        Bind(module, function.TypeParams, arguments);

    public static Dictionary<int, TypeId> Bind(Module module, IdRange typeParams, IReadOnlyList<int> arguments)
    {
        var bindings = new Dictionary<int, TypeId>();
        for (int i = 0; i < typeParams.Count && i < arguments.Count; i++)
        {
            bindings[module.TypeParams[typeParams.Start + i].Index] = new TypeId(arguments[i]);
        }

        return bindings;
    }
}
