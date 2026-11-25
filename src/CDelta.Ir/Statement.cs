namespace CDelta.Ir;

public readonly record struct Statement(
    StatementKind Kind,
    PlaceId Place,
    RvalueId Rvalue,
    OperandId Operand,
    int Aux,
    StringId Message,
    SourceSpan Span);
