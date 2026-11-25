namespace CDelta.Ir;

public enum StatementKind
{
    Assign,
    Call,
    Check,
    Live,
    Dead,
    TracePush,
    TraceSave,
    TraceRestore,
}
