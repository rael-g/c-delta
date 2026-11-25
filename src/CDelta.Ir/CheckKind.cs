namespace CDelta.Ir;

public enum CheckKind
{
    Bounds,
    Null,
    Optional,
    ResultValue,
    ResultError,
    Enum,
    Align,
    DivZero,
    Overlap,
}
