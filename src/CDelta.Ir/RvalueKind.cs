namespace CDelta.Ir;

public enum RvalueKind
{
    Use,
    Binop,
    Unop,
    Cast,
    Call,
    Addr,
    Aggregate,
    Len,
    SizeOf,
    AlignOf,
    OffsetOf,
}
