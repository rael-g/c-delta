namespace CDelta.Ir;

public sealed class ModuleBuilder(Module module)
{
    public Module Module { get; } = module;

    public SourceId AddSource(string fullPath, string displayPath)
    {
        Module.Sources.Add(new SourceFile(fullPath, displayPath));
        return new SourceId(Module.Sources.Count - 1);
    }

    public TypeId Primitive(TypeKind kind) => Module.Type(new TypeNode(kind, TypeFlags.None, 0, 0));

    public TypeId Pointer(TypeId element, TypeFlags flags) => Module.Type(new TypeNode(TypeKind.Ptr, flags, element.Index, 0));

    public TypeId SliceOf(TypeId element, bool isConst, bool isText = false) =>
        Module.Type(new TypeNode(TypeKind.Slice, (isConst ? TypeFlags.Const : TypeFlags.None) | (isText ? TypeFlags.Text : TypeFlags.None), element.Index, 0));

    public TypeId OptionalOf(TypeId payload) => Module.Type(new TypeNode(TypeKind.Optional, TypeFlags.None, payload.Index, 0));

    public TypeId ResultOf(TypeId value, TypeId error) => Module.Type(new TypeNode(TypeKind.Result, TypeFlags.None, value.Index, error.Index));

    public TypeId AnyError() => Module.Type(new TypeNode(TypeKind.AnyError, TypeFlags.None, 0, 0));

    public TypeId ErrorSetOf(IEnumerable<string> names)
    {
        string[] sorted = [.. names.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];
        int[] encoded = new int[sorted.Length];
        for (int i = 0; i < encoded.Length; i++)
        {
            encoded[i] = Module.Intern(sorted[i]).Index;
        }

        return Module.Type(new TypeNode(TypeKind.ErrorSet, TypeFlags.None, Module.TypeList(encoded).Index, 0));
    }

    public ConstId ErrorConstant(TypeId type, string name) =>
        Module.Constant(new ConstNode(ConstKind.Error, type, Module.Intern(name).Index, BytesId.None, TypeListId.None));

    public TypeId ArrayOf(TypeId element, int length) => Module.Type(new TypeNode(TypeKind.Array, TypeFlags.None, element.Index, length));

    public SigId Signature(CallConv conv, IReadOnlyList<SigParam> parameters, TypeId result, bool variadic, ParamMode returnMode = ParamMode.Value)
    {
        int[] encoded = new int[parameters.Count];
        for (int i = 0; i < encoded.Length; i++)
        {
            encoded[i] = parameters[i].Encode();
        }

        return Module.Sig(new SigNode(conv, Module.TypeList(encoded), result, returnMode, variadic));
    }

    public TypeId FunctionPointer(SigId sig) => Module.Type(new TypeNode(TypeKind.Fn, TypeFlags.None, sig.Index, 0));

    public ConstId IntConstant(TypeId type, Int128 value) =>
        Module.Constant(new ConstNode(ConstKind.Int, type, value, BytesId.None, TypeListId.None));

    public ConstId FloatConstant(TypeId type, ulong bits) =>
        Module.Constant(new ConstNode(ConstKind.Float, type, bits, BytesId.None, TypeListId.None));

    public ConstId BoolConstant(TypeId type, bool value) =>
        Module.Constant(new ConstNode(ConstKind.Bool, type, value ? 1 : 0, BytesId.None, TypeListId.None));

    public ConstId BytesConstant(TypeId type, byte[] data) =>
        Module.Constant(new ConstNode(ConstKind.Bytes, type, 0, Module.InternBytes(data), TypeListId.None));

    public ConstId AggregateConstant(TypeId type, IReadOnlyList<ConstId> items)
    {
        int[] encoded = new int[items.Count];
        for (int i = 0; i < encoded.Length; i++)
        {
            encoded[i] = items[i].Index;
        }

        return Module.Constant(new ConstNode(ConstKind.Aggregate, type, 0, BytesId.None, Module.TypeList(encoded)));
    }

    public ConstId UndefConstant(TypeId type) => Module.Constant(new ConstNode(ConstKind.Undef, type, 0, BytesId.None, TypeListId.None));

    public GlobalId DeclareGlobal(
        string key,
        string name,
        SourceSpan span,
        Origin origin,
        TypeId type,
        bool mutable,
        bool threadLocal,
        ConstId initializer,
        LinkageKind linkage,
        string? exportName)
    {
        var decl = new GlobalDecl(
            Module.Intern(key),
            Module.Intern(name),
            span,
            origin,
            IdRange.Empty,
            type,
            mutable,
            threadLocal,
            initializer,
            linkage,
            exportName is null ? StringId.None : Module.Intern(exportName),
            StringId.None,
            0);
        var id = new GlobalId(Module.Globals.Count);
        Module.Globals.Add(decl);
        Module.GlobalsByKey.Add(key, id);
        return id;
    }

    public ConstId NullConstant(TypeId type) => Module.Constant(new ConstNode(ConstKind.Null, type, 0, BytesId.None, TypeListId.None));

    public EnumId DeclareEnum(
        string key,
        string name,
        SourceSpan span,
        Origin origin,
        TypeId underlying,
        bool exhaustive,
        IReadOnlyList<(string Name, Int128 Value)> members)
    {
        int start = Module.EnumMembers.Count;
        foreach ((string memberName, Int128 value) in members)
        {
            Module.EnumMembers.Add(new EnumMember(Module.Intern(memberName), value));
        }

        var id = new EnumId(Module.Enums.Count);
        Module.Enums.Add(new EnumDecl(
            Module.Intern(key),
            Module.Intern(name),
            span,
            origin,
            IdRange.Empty,
            underlying,
            exhaustive,
            new IdRange(start, members.Count)));
        Module.EnumsByKey.Add(key, id);
        return id;
    }

    public OpaqueId DeclareOpaque(string key, string name, SourceSpan span, Origin origin)
    {
        var id = new OpaqueId(Module.Opaques.Count);
        Module.Opaques.Add(new OpaqueDecl(Module.Intern(key), Module.Intern(name), span, origin, IdRange.Empty));
        Module.OpaquesByKey.Add(key, id);
        return id;
    }

    public TypeId OpaqueType(OpaqueId id) => Module.Type(new TypeNode(TypeKind.Opaque, TypeFlags.None, id.Index, 0));

    public int AddCImport(CImportDecl import)
    {
        Module.CImports.Add(import);
        return Module.CImports.Count - 1;
    }

    public TypeId EnumType(EnumId id) => Module.Type(new TypeNode(TypeKind.Enum, TypeFlags.None, id.Index, 0));

    public StructId DeclareStruct(
        string key,
        string name,
        SourceSpan span,
        Origin origin,
        IReadOnlyList<FieldDecl> fields,
        StructLayoutKind layout,
        int pack,
        int minSize,
        IReadOnlyList<string> attributes)
    {
        int fieldStart = Module.Fields.Count;
        Module.Fields.AddRange(fields);
        int attrStart = Module.Attributes.Count;
        foreach (string attribute in attributes)
        {
            Module.Attributes.Add(new AttributeNode(Module.Intern(attribute), IdRange.Empty));
        }

        var decl = new StructDecl(
            Module.Intern(key),
            Module.Intern(name),
            span,
            origin,
            new IdRange(attrStart, attributes.Count),
            new IdRange(fieldStart, fields.Count),
            layout,
            pack,
            minSize,
            StructDecl.NotComputed,
            StructDecl.NotComputed);
        var id = new StructId(Module.Structs.Count);
        Module.Structs.Add(decl);
        Module.StructsByKey.Add(key, id);
        return id;
    }

    public StructId BeginStruct(
        string key,
        string name,
        SourceSpan span,
        Origin origin,
        StructLayoutKind layout,
        int pack,
        int minSize,
        IReadOnlyList<string> attributes) =>
        DeclareStruct(key, name, span, origin, [], layout, pack, minSize, attributes);

    public void DefineFields(StructId id, IReadOnlyList<FieldDecl> fields)
    {
        int start = Module.Fields.Count;
        Module.Fields.AddRange(fields);
        Module.Structs[id.Index] = Module.Structs[id.Index] with { Fields = new IdRange(start, fields.Count) };
    }

    public TypeId StructType(StructId id) => Module.Type(new TypeNode(TypeKind.Struct, TypeFlags.None, id.Index, 0));

    public FunctionId DeclareFunction(
        string key,
        string name,
        SourceSpan span,
        Origin origin,
        IReadOnlyList<ParamDecl> parameters,
        bool variadic,
        TypeId result,
        CallConv conv,
        LinkageKind linkage,
        string? exportName,
        IReadOnlyList<string> attributes,
        ParamMode returnMode = ParamMode.Value)
    {
        int paramStart = Module.Params.Count;
        Module.Params.AddRange(parameters);

        int attrStart = Module.Attributes.Count;
        foreach (string attribute in attributes)
        {
            Module.Attributes.Add(new AttributeNode(Module.Intern(attribute), IdRange.Empty));
        }

        var decl = new FunctionDecl(
            Module.Intern(key),
            Module.Intern(name),
            span,
            origin,
            new IdRange(attrStart, attributes.Count),
            new IdRange(paramStart, parameters.Count),
            variadic,
            result,
            returnMode,
            conv,
            linkage,
            exportName is null ? StringId.None : Module.Intern(exportName),
            false,
            InlineKind.Default,
            SafetyOverride.Default);
        var id = new FunctionId(Module.Functions.Count);
        Module.Functions.Add(decl);
        Module.Bodies.Add(null);
        Module.FunctionsByKey.Add(key, id);
        return id;
    }

    public void SetBody(FunctionId function, Body body) => Module.Bodies[function.Index] = body;
}
