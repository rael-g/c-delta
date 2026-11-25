namespace CDelta.Ir;

public static class IrTypes
{
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

    public static string Name(TypeKind kind) => kind switch
    {
        TypeKind.Void => "void",
        TypeKind.NoReturn => "noreturn",
        TypeKind.Bool => "bool",
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
