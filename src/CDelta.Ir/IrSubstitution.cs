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

    public static Dictionary<int, TypeId> Bind(Module module, FunctionDecl function, IReadOnlyList<int> arguments)
    {
        var bindings = new Dictionary<int, TypeId>();
        for (int i = 0; i < function.TypeParams.Count && i < arguments.Count; i++)
        {
            bindings[module.TypeParams[function.TypeParams.Start + i].Index] = new TypeId(arguments[i]);
        }

        return bindings;
    }
}
