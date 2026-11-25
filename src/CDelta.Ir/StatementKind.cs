namespace CDelta.Ir;

public enum StatementKind
{
    Assign,
    Call,
    Check,
    Live,
    Dead,
    Intrinsic,
    TracePush,
    TraceSave,
    TraceRestore,
}
