using CDelta.Ir;

namespace CDelta.Core;

internal sealed class ModuleRemapper(Module source, Module destination)
{
    private readonly Dictionary<int, StructId> _structs = [];

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
            case TypeKind.Struct:
                return destination.Type(node with { A = Struct(new StructId(node.A)).Index });
            default:
                return destination.Type(node);
        }
    }

    public StructId Struct(StructId id)
    {
        if (_structs.TryGetValue(id.Index, out StructId existing))
        {
            return existing;
        }

        StructDecl declaration = source.Structs[id.Index];
        var created = new StructId(destination.Structs.Count);
        destination.Structs.Add(declaration);
        destination.StructsByKey.Add(source.Text(declaration.Key), created);
        _structs.Add(id.Index, created);

        var fields = new List<FieldDecl>();
        for (int i = 0; i < declaration.Fields.Count; i++)
        {
            FieldDecl field = source.Fields[declaration.Fields.Start + i];
            fields.Add(field with { Name = String(field.Name), Type = Type(field.Type), Attributes = IdRange.Empty });
        }

        int fieldStart = destination.Fields.Count;
        destination.Fields.AddRange(fields);
        int attributeStart = destination.Attributes.Count;
        for (int i = 0; i < declaration.Attributes.Count; i++)
        {
            AttributeNode attribute = source.Attributes[declaration.Attributes.Start + i];
            destination.Attributes.Add(new AttributeNode(String(attribute.Name), IdRange.Empty));
        }

        destination.Structs[created.Index] = declaration with
        {
            Key = String(declaration.Key),
            Name = String(declaration.Name),
            Origin = Origin(declaration.Origin),
            Fields = new IdRange(fieldStart, fields.Count),
            Attributes = new IdRange(attributeStart, declaration.Attributes.Count),
        };
        return created;
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

        return destination.Sig(new SigNode(node.Conv, destination.TypeList(mapped), Type(node.Return), node.ReturnMode, node.Variadic));
    }

    public ConstId Constant(ConstId id)
    {
        ConstNode node = source.Constants[id.Index];
        BytesId bytes = node.Bytes.IsNone ? node.Bytes : destination.InternBytes(source.Bytes[node.Bytes.Index]);
        TypeListId items = node.Items;
        if (!items.IsNone)
        {
            int[] original = source.TypeLists[items.Index];
            int[] mapped = new int[original.Length];
            for (int i = 0; i < original.Length; i++)
            {
                mapped[i] = Constant(new ConstId(original[i])).Index;
            }

            items = destination.TypeList(mapped);
        }

        return destination.Constant(node with { Type = Type(node.Type), Bytes = bytes, Items = items });
    }

    public Origin Origin(Origin origin) =>
        origin with { Language = String(origin.Language), Library = String(origin.Library), CName = String(origin.CName) };
}
