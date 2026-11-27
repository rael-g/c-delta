namespace CDelta.Ir;

public static class IrTypes
{
    public static TypeKind ScalarKind(Module module, TypeId type)
    {
        TypeNode node = module.Types[type.Index];
        return node.Kind == TypeKind.Enum ? ScalarKind(module, module.Enums[node.A].Underlying) : node.Kind;
    }

    public static bool IsInteger(TypeKind kind) => kind is >= TypeKind.I8 and <= TypeKind.USize
        or >= TypeKind.CChar and <= TypeKind.CULongLong;

    public static bool IsFloat(TypeKind kind) => kind is TypeKind.F16 or TypeKind.F32 or TypeKind.F64;

    public static bool IsSigned(TypeKind kind, Target? target) => kind switch
    {
        TypeKind.I8 or TypeKind.I16 or TypeKind.I32 or TypeKind.I64 or TypeKind.I128 or TypeKind.ISize => true,
        TypeKind.CSChar or TypeKind.CShort or TypeKind.CInt or TypeKind.CLong or TypeKind.CLongLong => true,
        TypeKind.CChar => target?.CharIsSigned ?? true,
        _ => false,
    };

    public static int BitWidth(TypeKind kind, Target target) => kind switch
    {
        TypeKind.I8 or TypeKind.U8 or TypeKind.CChar or TypeKind.CSChar or TypeKind.CUChar => 8,
        TypeKind.I16 or TypeKind.U16 or TypeKind.CShort or TypeKind.CUShort => 16,
        TypeKind.I32 or TypeKind.U32 or TypeKind.CInt or TypeKind.CUInt => 32,
        TypeKind.I64 or TypeKind.U64 or TypeKind.CLongLong or TypeKind.CULongLong => 64,
        TypeKind.I128 or TypeKind.U128 => 128,
        TypeKind.ISize or TypeKind.USize => target.PointerSize * 8,
        TypeKind.CLong or TypeKind.CULong => target.CLongSize * 8,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    public static int[] StructTypeArgs(Module module, TypeNode node) =>
        node.B > 0 ? module.TypeLists[node.B - 1] : [];

    public static List<(TypeId Owner, InterfaceMethod Method)> FlatMethods(Module module, TypeId interfaceType)
    {
        var result = new List<(TypeId Owner, InterfaceMethod Method)>();
        var seen = new HashSet<int>();
        Visit(module, interfaceType, seen, result);
        return result;
    }

    private static void Visit(Module module, TypeId type, HashSet<int> seen, List<(TypeId Owner, InterfaceMethod Method)> result)
    {
        if (!seen.Add(type.Index))
        {
            return;
        }

        TypeNode node = module.Types[type.Index];
        InterfaceDecl declaration = module.Interfaces[node.A];
        Dictionary<int, TypeId> bindings = IrSubstitution.Bind(module, declaration.TypeParams, StructTypeArgs(module, node));
        for (int i = 0; i < declaration.Bases.Count; i++)
        {
            Visit(module, IrSubstitution.Apply(module, module.InterfaceBases[declaration.Bases.Start + i], bindings), seen, result);
        }

        for (int i = 0; i < declaration.Methods.Count; i++)
        {
            result.Add((type, module.InterfaceMethods[declaration.Methods.Start + i]));
        }
    }

    public static (List<TypeId> Parameters, TypeId Return, InterfaceMethod Method)? InterfaceMethodSignature(Module module, TypeId interfaceType, int method)
    {
        List<(TypeId Owner, InterfaceMethod Method)> flat = FlatMethods(module, interfaceType);
        if (method < 0 || method >= flat.Count)
        {
            return null;
        }

        (TypeId owner, InterfaceMethod item) = flat[method];
        TypeNode node = module.Types[owner.Index];
        InterfaceDecl declaration = module.Interfaces[node.A];
        SigNode sig = module.Sigs[item.Sig.Index];
        Dictionary<int, TypeId> bindings = IrSubstitution.Bind(module, declaration.TypeParams, StructTypeArgs(module, node));
        var parameters = new List<TypeId>();
        foreach (int encoded in module.TypeLists[sig.Params.Index])
        {
            parameters.Add(IrSubstitution.Apply(module, SigParam.Decode(encoded).Type, bindings));
        }

        return (parameters, IrSubstitution.Apply(module, sig.Return, bindings), item);
    }

    public static int[] CallTypeArgs(Module module, Rvalue call) =>
        call.Aux > 0 ? module.TypeLists[call.Aux - 1] : [];

    public static string Name(TypeKind kind) => kind switch
    {
        TypeKind.Void => "void",
        TypeKind.NoReturn => "noreturn",
        TypeKind.Bool => "bool",
        TypeKind.ComptimeType => "comptime.type",
        TypeKind.I8 => "i8",
        TypeKind.I16 => "i16",
        TypeKind.I32 => "i32",
        TypeKind.I64 => "i64",
        TypeKind.I128 => "i128",
        TypeKind.U8 => "u8",
        TypeKind.U16 => "u16",
        TypeKind.U32 => "u32",
        TypeKind.U64 => "u64",
        TypeKind.U128 => "u128",
        TypeKind.ISize => "isize",
        TypeKind.USize => "usize",
        TypeKind.F16 => "f16",
        TypeKind.F32 => "f32",
        TypeKind.F64 => "f64",
        TypeKind.CChar => "c.char",
        TypeKind.CSChar => "c.schar",
        TypeKind.CUChar => "c.uchar",
        TypeKind.CShort => "c.short",
        TypeKind.CUShort => "c.ushort",
        TypeKind.CInt => "c.int",
        TypeKind.CUInt => "c.uint",
        TypeKind.CLong => "c.long",
        TypeKind.CULong => "c.ulong",
        TypeKind.CLongLong => "c.longlong",
        TypeKind.CULongLong => "c.ulonglong",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };
}
