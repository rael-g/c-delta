using CDelta.Ir;

namespace CDelta.Backend.C;

internal static class CTypes
{
    public static string Spell(Module module, TypeId type)
    {
        TypeNode node = module.Types[type.Index];
        switch (node.Kind)
        {
            case TypeKind.Ptr:
                string element = Spell(module, new TypeId(node.A));
                string qualifiers = (node.Flags.HasFlag(TypeFlags.Const) ? "const " : string.Empty)
                    + (node.Flags.HasFlag(TypeFlags.Volatile) ? "volatile " : string.Empty);
                return qualifiers + element + "*";
            default:
                return Primitive(node.Kind);
        }
    }

    public static string Primitive(TypeKind kind) => kind switch
    {
        TypeKind.Void or TypeKind.NoReturn => "void",
        TypeKind.Bool => "bool",
        TypeKind.I8 => "int8_t",
        TypeKind.I16 => "int16_t",
        TypeKind.I32 => "int32_t",
        TypeKind.I64 => "int64_t",
        TypeKind.I128 => "__int128",
        TypeKind.U8 => "uint8_t",
        TypeKind.U16 => "uint16_t",
        TypeKind.U32 => "uint32_t",
        TypeKind.U64 => "uint64_t",
        TypeKind.U128 => "unsigned __int128",
        TypeKind.ISize => "intptr_t",
        TypeKind.USize => "uintptr_t",
        TypeKind.F32 => "float",
        TypeKind.F64 => "double",
        TypeKind.CChar => "char",
        TypeKind.CSChar => "signed char",
        TypeKind.CUChar => "unsigned char",
        TypeKind.CShort => "short",
        TypeKind.CUShort => "unsigned short",
        TypeKind.CInt => "int",
        TypeKind.CUInt => "unsigned int",
        TypeKind.CLong => "long",
        TypeKind.CULong => "unsigned long",
        TypeKind.CLongLong => "long long",
        TypeKind.CULongLong => "unsigned long long",
        _ => throw new NotSupportedException("the type " + kind + " has no C spelling at this stage"),
    };

    public static string Unsigned(TypeKind kind) => kind switch
    {
        TypeKind.I8 or TypeKind.U8 => "uint8_t",
        TypeKind.I16 or TypeKind.U16 => "uint16_t",
        TypeKind.I32 or TypeKind.U32 => "uint32_t",
        TypeKind.I64 or TypeKind.U64 => "uint64_t",
        TypeKind.I128 or TypeKind.U128 => "unsigned __int128",
        TypeKind.ISize or TypeKind.USize => "uintptr_t",
        TypeKind.CChar or TypeKind.CSChar or TypeKind.CUChar => "unsigned char",
        TypeKind.CShort or TypeKind.CUShort => "unsigned short",
        TypeKind.CInt or TypeKind.CUInt => "unsigned int",
        TypeKind.CLong or TypeKind.CULong => "unsigned long",
        TypeKind.CLongLong or TypeKind.CULongLong => "unsigned long long",
        _ => throw new NotSupportedException("the type " + kind + " has no unsigned counterpart"),
    };
}
