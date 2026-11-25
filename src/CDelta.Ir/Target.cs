namespace CDelta.Ir;

public readonly record struct Target(
    string Triple,
    int PointerSize,
    int CLongSize,
    TypeKind WCharType,
    bool CharIsSigned,
    bool NativeInt128,
    bool NativeF16,
    BitFieldRules BitFields);
