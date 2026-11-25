using CDelta.Ir;

namespace CDelta.Core;

internal sealed class ModuleRemapper(Module source, Module destination)
{
    public StringId String(StringId id) => id.IsNone ? id : destination.Intern(source.Text(id));

    public TypeId Type(TypeId id)
    {
        if (id.IsNone)
        {
            return id;
        }

        TypeNode node = source.Types[id.Index];
        switch (node.Kind)
        {
            case TypeKind.Ptr:
            case TypeKind.Slice:
            case TypeKind.Array:
                return destination.Type(node with { A = Type(new TypeId(node.A)).Index });
            case TypeKind.Fn:
                return destination.Type(node with { A = Sig(new SigId(node.A)).Index });
            default:
                return destination.Type(node);
        }
    }

    public SigId Sig(SigId id)
    {
        SigNode node = source.Sigs[id.Index];
        int[] items = source.TypeLists[node.Params.Index];
        int[] mapped = new int[items.Length];
        for (int i = 0; i < items.Length; i++)
        {
            var parameter = SigParam.Decode(items[i]);
            mapped[i] = new SigParam(Type(parameter.Type), parameter.Mode).Encode();
        }

        return destination.Sig(new SigNode(node.Conv, destination.TypeList(mapped), Type(node.Return), node.Variadic));
    }

    public ConstId Constant(ConstId id)
    {
        ConstNode node = source.Constants[id.Index];
        BytesId bytes = node.Bytes.IsNone ? node.Bytes : destination.InternBytes(source.Bytes[node.Bytes.Index]);
        return destination.Constant(node with { Type = Type(node.Type), Bytes = bytes });
    }

    public Origin Origin(Origin origin) =>
        origin with { Language = String(origin.Language), Library = String(origin.Library), CName = String(origin.CName) };
}
