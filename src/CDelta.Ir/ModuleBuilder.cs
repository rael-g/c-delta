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

    public TypeId SliceOf(TypeId element, bool isConst) =>
        Module.Type(new TypeNode(TypeKind.Slice, isConst ? TypeFlags.Const : TypeFlags.None, element.Index, 0));

    public TypeId ArrayOf(TypeId element, int length) => Module.Type(new TypeNode(TypeKind.Array, TypeFlags.None, element.Index, length));

    public SigId Signature(CallConv conv, IReadOnlyList<SigParam> parameters, TypeId result, bool variadic)
    {
        int[] encoded = new int[parameters.Count];
        for (int i = 0; i < encoded.Length; i++)
        {
            encoded[i] = parameters[i].Encode();
        }

        return Module.Sig(new SigNode(conv, Module.TypeList(encoded), result, variadic));
    }

    public TypeId FunctionPointer(SigId sig) => Module.Type(new TypeNode(TypeKind.Fn, TypeFlags.None, sig.Index, 0));

    public ConstId IntConstant(TypeId type, Int128 value) =>
        Module.Constant(new ConstNode(ConstKind.Int, type, value, BytesId.None));

    public ConstId BoolConstant(TypeId type, bool value) =>
        Module.Constant(new ConstNode(ConstKind.Bool, type, value ? 1 : 0, BytesId.None));

    public ConstId BytesConstant(TypeId type, byte[] data) =>
        Module.Constant(new ConstNode(ConstKind.Bytes, type, 0, Module.InternBytes(data)));

    public ConstId NullConstant(TypeId type) => Module.Constant(new ConstNode(ConstKind.Null, type, 0, BytesId.None));

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
        IReadOnlyList<string> attributes)
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
