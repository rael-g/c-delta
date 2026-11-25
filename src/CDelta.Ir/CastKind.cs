namespace CDelta.Ir;

public enum CastKind
{
    IntInt,
    IntFloat,
    FloatInt,
    FloatFloat,
    IntEnum,
    EnumInt,
    PtrInt,
    IntPtr,
    PtrPtr,
    Bitcast,
    SlicePtr,
}
