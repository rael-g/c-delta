namespace CDelta.Ir;

public readonly record struct Rvalue(
    RvalueKind Kind,
    int Op,
    ArithMode Mode,
    OperandId A,
    OperandId B,
    TypeId Type,
    CalleeKind Callee,
    int CalleeId,
    IdRange Args);
