namespace CDelta.Ir;

public sealed class Module(string name)
{
    private readonly Dictionary<string, StringId> _stringIds = [];
    private readonly Dictionary<TypeNode, TypeId> _typeIds = [];
    private readonly Dictionary<SigNode, SigId> _sigIds = [];
    private readonly Dictionary<string, TypeListId> _typeListIds = [];
    private readonly Dictionary<ConstNode, ConstId> _constIds = [];
    private readonly Dictionary<string, BytesId> _bytesIds = [];

    public string Name { get; } = name;

    public ModuleStage Stage { get; set; } = ModuleStage.Lowered;

    public Target? Target { get; set; }

    public List<SourceFile> Sources { get; } = [];

    public List<string> Strings { get; } = [];

    public List<TypeNode> Types { get; } = [];

    public List<SigNode> Sigs { get; } = [];

    public List<int[]> TypeLists { get; } = [];

    public List<ConstNode> Constants { get; } = [];

    public List<byte[]> Bytes { get; } = [];

    public SortedDictionary<string, int> ErrorNames { get; } = new(StringComparer.Ordinal);

    public List<AttributeNode> Attributes { get; } = [];

    public List<AttributeArg> AttributeArgs { get; } = [];

    public List<ParamDecl> Params { get; } = [];

    public List<StringId> TypeParams { get; } = [];

    public List<FieldDecl> Fields { get; } = [];

    public List<EnumMember> EnumMembers { get; } = [];

    public List<EnumDecl> Enums { get; } = [];

    public Dictionary<string, EnumId> EnumsByKey { get; } = [];

    public List<OpaqueDecl> Opaques { get; } = [];

    public Dictionary<string, OpaqueId> OpaquesByKey { get; } = [];

    public List<UnionDecl> Unions { get; } = [];

    public List<TypeId> UnionMembers { get; } = [];

    public Dictionary<string, UnionId> UnionsByKey { get; } = [];

    public List<CImportDecl> CImports { get; } = [];

    public List<InterfaceDecl> Interfaces { get; } = [];

    public List<InterfaceMethod> InterfaceMethods { get; } = [];

    public Dictionary<string, InterfaceId> InterfacesByKey { get; } = [];

    public List<ImplDecl> Impls { get; } = [];

    public List<ImplMethod> ImplMethods { get; } = [];

    public List<ConstrainedRef> ConstrainedRefs { get; } = [];

    public List<StructDecl> Structs { get; } = [];

    public Dictionary<string, StructId> StructsByKey { get; } = [];

    public List<GlobalDecl> Globals { get; } = [];

    public Dictionary<string, GlobalId> GlobalsByKey { get; } = [];

    public List<FunctionDecl> Functions { get; } = [];

    public List<Body?> Bodies { get; } = [];

    public Dictionary<string, FunctionId> FunctionsByKey { get; } = [];

    public StringId Intern(string value)
    {
        if (_stringIds.TryGetValue(value, out StringId existing))
        {
            return existing;
        }

        var id = new StringId(Strings.Count);
        Strings.Add(value);
        _stringIds.Add(value, id);
        return id;
    }

    public string Text(StringId id) => Strings[id.Index];

    public TypeId Type(TypeNode node)
    {
        if (_typeIds.TryGetValue(node, out TypeId existing))
        {
            return existing;
        }

        var id = new TypeId(Types.Count);
        Types.Add(node);
        _typeIds.Add(node, id);
        return id;
    }

    public SigId Sig(SigNode node)
    {
        if (_sigIds.TryGetValue(node, out SigId existing))
        {
            return existing;
        }

        var id = new SigId(Sigs.Count);
        Sigs.Add(node);
        _sigIds.Add(node, id);
        return id;
    }

    public TypeListId TypeList(int[] items)
    {
        string key = string.Join(',', items);
        if (_typeListIds.TryGetValue(key, out TypeListId existing))
        {
            return existing;
        }

        var id = new TypeListId(TypeLists.Count);
        TypeLists.Add(items);
        _typeListIds.Add(key, id);
        return id;
    }

    public ConstId Constant(ConstNode node)
    {
        if (_constIds.TryGetValue(node, out ConstId existing))
        {
            return existing;
        }

        var id = new ConstId(Constants.Count);
        Constants.Add(node);
        _constIds.Add(node, id);
        return id;
    }

    public BytesId InternBytes(byte[] data)
    {
        string key = Convert.ToBase64String(data);
        if (_bytesIds.TryGetValue(key, out BytesId existing))
        {
            return existing;
        }

        var id = new BytesId(Bytes.Count);
        Bytes.Add(data);
        _bytesIds.Add(key, id);
        return id;
    }
}
