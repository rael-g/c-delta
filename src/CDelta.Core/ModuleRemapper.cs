using CDelta.Ir;

namespace CDelta.Core;

internal sealed class ModuleRemapper(Module source, Module destination)
{
    private readonly Dictionary<int, StructId> _structs = [];
    private readonly Dictionary<int, EnumId> _enums = [];
    private readonly Dictionary<string, StructId> _structInstances = [];

    public Dictionary<StructId, (StructId Source, IReadOnlyList<TypeId> Arguments)> Instances { get; } = [];

    public Dictionary<int, TypeId>? Bindings { get; set; }

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
            case TypeKind.Optional:
            case TypeKind.Array:
                return destination.Type(node with { A = Type(new TypeId(node.A)).Index });
            case TypeKind.Result:
                return destination.Type(node with { A = Type(new TypeId(node.A)).Index, B = Type(new TypeId(node.B)).Index });
            case TypeKind.ErrorSet:
                int[] names = source.TypeLists[node.A].Select(index => destination.Intern(source.Strings[index]).Index).ToArray();
                return destination.Type(node with { A = destination.TypeList(names).Index });
            case TypeKind.Fn:
            case TypeKind.Closure:
                return destination.Type(node with { A = Sig(new SigId(node.A)).Index });
            case TypeKind.Struct:
                int[] sourceArgs = IrTypes.StructTypeArgs(source, node);
                if (sourceArgs.Length == 0)
                {
                    return destination.Type(node with { A = Struct(new StructId(node.A)).Index });
                }

                var mappedArgs = sourceArgs.Select(arg => Type(new TypeId(arg))).ToList();
                return new ModuleBuilder(destination).StructType(StructInstance(new StructId(node.A), mappedArgs));
            case TypeKind.Enum:
                return destination.Type(node with { A = Enum(new EnumId(node.A)).Index });
            case TypeKind.Opaque:
                return destination.Type(node with { A = Opaque(new OpaqueId(node.A)).Index });
            case TypeKind.Union:
                return destination.Type(node with { A = Union(new UnionId(node.A)).Index });
            case TypeKind.Interface:
                return new ModuleBuilder(destination).InterfaceType(InterfaceInstance(new InterfaceId(node.A), IrTypes.StructTypeArgs(source, node).Select(argument => Type(new TypeId(argument))).ToList()));
            case TypeKind.Dyn:
                return new ModuleBuilder(destination).DynOf(Type(new TypeId(node.A)));
            case TypeKind.Param:
                if (Bindings is not null && Bindings.TryGetValue(node.A, out TypeId bound))
                {
                    return bound;
                }

                throw new InvalidOperationException("the type parameter " + source.Strings[node.A] + " is not bound");
            default:
                return destination.Type(node);
        }
    }

    public EnumId Enum(EnumId id)
    {
        if (_enums.TryGetValue(id.Index, out EnumId existing))
        {
            return existing;
        }

        EnumDecl declaration = source.Enums[id.Index];
        var members = new List<(string Name, Int128 Value)>();
        for (int i = 0; i < declaration.Members.Count; i++)
        {
            EnumMember member = source.EnumMembers[declaration.Members.Start + i];
            members.Add((source.Text(member.Name), member.Value));
        }

        var builder = new ModuleBuilder(destination);
        EnumId created = builder.DeclareEnum(
            source.Text(declaration.Key),
            source.Text(declaration.Name),
            declaration.Span,
            Origin(declaration.Origin),
            Type(declaration.Underlying),
            declaration.Exhaustive,
            members);
        _enums.Add(id.Index, created);
        return created;
    }

    public UnionId Union(UnionId id)
    {
        UnionDecl declaration = source.Unions[id.Index];
        string key = source.Text(declaration.Key);
        if (destination.UnionsByKey.TryGetValue(key, out UnionId existing))
        {
            return existing;
        }

        var builder = new ModuleBuilder(destination);
        UnionId created = builder.DeclareUnion(key, source.Text(declaration.Name), declaration.Span, Type(declaration.Interface));
        var members = new List<TypeId>();
        for (int i = 0; i < declaration.Members.Count; i++)
        {
            members.Add(Type(source.UnionMembers[declaration.Members.Start + i]));
        }

        builder.DefineUnionMembers(created, members);
        return created;
    }

    public OpaqueId Opaque(OpaqueId id)
    {
        OpaqueDecl declaration = source.Opaques[id.Index];
        string key = source.Text(declaration.Key);
        if (destination.OpaquesByKey.TryGetValue(key, out OpaqueId existing))
        {
            return existing;
        }

        return new ModuleBuilder(destination).DeclareOpaque(key, source.Text(declaration.Name), declaration.Span, Origin(declaration.Origin));
    }

    private StructId StructInstance(StructId id, IReadOnlyList<TypeId> typeArguments)
    {
        string instanceKey = id.Index + "|" + string.Join(",", typeArguments.Select(argument => argument.Index));
        if (_structInstances.TryGetValue(instanceKey, out StructId existing))
        {
            return existing;
        }

        StructDecl declaration = source.Structs[id.Index];
        string name = source.Text(declaration.Key) + "<"
            + string.Join(", ", typeArguments.Select(argument => IrPrinter.TypeText(destination, argument))) + ">";
        var created = new StructId(destination.Structs.Count);
        destination.Structs.Add(declaration);
        destination.StructsByKey.Add(name, created);
        _structInstances.Add(instanceKey, created);
        Instances.Add(created, (id, typeArguments));

        var bindings = new Dictionary<int, TypeId>();
        for (int i = 0; i < declaration.TypeParams.Count; i++)
        {
            bindings[source.TypeParams[declaration.TypeParams.Start + i].Index] = typeArguments[i];
        }

        Dictionary<int, TypeId>? saved = Bindings;
        Bindings = bindings;
        var fields = new List<FieldDecl>();
        for (int i = 0; i < declaration.Fields.Count; i++)
        {
            FieldDecl field = source.Fields[declaration.Fields.Start + i];
            fields.Add(field with { Name = String(field.Name), Type = Type(field.Type), Attributes = IdRange.Empty });
        }

        Bindings = saved;
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
            Key = destination.Intern(name),
            Name = destination.Intern(name),
            Origin = Origin(declaration.Origin),
            Fields = new IdRange(fieldStart, fields.Count),
            Attributes = new IdRange(attributeStart, declaration.Attributes.Count),
            TypeParams = IdRange.Empty,
        };
        return created;
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

    private InterfaceId InterfaceInstance(InterfaceId id, IReadOnlyList<TypeId> typeArguments)
    {
        InterfaceDecl declaration = source.Interfaces[id.Index];
        string name = source.Text(declaration.Key) + (typeArguments.Count == 0
            ? string.Empty
            : "<" + string.Join(", ", typeArguments.Select(argument => IrPrinter.TypeText(destination, argument))) + ">");
        if (destination.InterfacesByKey.TryGetValue(name, out InterfaceId existing))
        {
            return existing;
        }

        var builder = new ModuleBuilder(destination);
        InterfaceId created = builder.DeclareInterface(name, name, declaration.Span, []);
        var bindings = new Dictionary<int, TypeId>();
        for (int i = 0; i < declaration.TypeParams.Count; i++)
        {
            bindings[source.TypeParams[declaration.TypeParams.Start + i].Index] = typeArguments[i];
        }

        Dictionary<int, TypeId>? saved = Bindings;
        Bindings = bindings;
        var methods = new List<InterfaceMethod>();
        for (int i = 0; i < declaration.Methods.Count; i++)
        {
            InterfaceMethod method = source.InterfaceMethods[declaration.Methods.Start + i];
            methods.Add(new InterfaceMethod(String(method.Name), method.IsStatic, Sig(method.Sig), FunctionId.None));
        }

        Bindings = saved;
        builder.DefineInterfaceMethods(created, methods);
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

        Int128 value = node.Kind switch
        {
            ConstKind.Error => destination.Intern(source.Strings[(int)node.Value]).Index,
            ConstKind.Type => Type(new TypeId((int)node.Value)).Index,
            _ => node.Value,
        };
        return destination.Constant(node with { Type = Type(node.Type), Value = value, Bytes = bytes, Items = items });
    }

    public Origin Origin(Origin origin) =>
        origin with { Language = String(origin.Language), Library = String(origin.Library), CName = String(origin.CName) };
}
