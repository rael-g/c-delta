using CDelta.Ir;

namespace CDelta.Core;

public static class PrimitiveLayout
{
    private const int Int128Size = 16;
    private const int NativeInt128Alignment = 16;
    private const int EmulatedInt128Alignment = 8;

    public static int Size(TypeKind kind, Target target) => kind switch
    {
        TypeKind.Void or TypeKind.NoReturn => 0,
        TypeKind.Bool => 1,
        TypeKind.I8 or TypeKind.U8 or TypeKind.CChar or TypeKind.CSChar or TypeKind.CUChar => 1,
        TypeKind.I16 or TypeKind.U16 or TypeKind.CShort or TypeKind.CUShort or TypeKind.F16 => 2,
        TypeKind.I32 or TypeKind.U32 or TypeKind.CInt or TypeKind.CUInt or TypeKind.F32 => 4,
        TypeKind.I64 or TypeKind.U64 or TypeKind.CLongLong or TypeKind.CULongLong or TypeKind.F64 => 8,
        TypeKind.I128 or TypeKind.U128 => Int128Size,
        TypeKind.ISize or TypeKind.USize or TypeKind.Ptr => target.PointerSize,
        TypeKind.CLong or TypeKind.CULong => target.CLongSize,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    public static int Alignment(TypeKind kind, Target target)
    {
        if (kind is TypeKind.I128 or TypeKind.U128)
        {
            return target.NativeInt128 ? NativeInt128Alignment : EmulatedInt128Alignment;
        }

        return kind is TypeKind.Void or TypeKind.NoReturn ? 1 : Size(kind, target);
    }
}
