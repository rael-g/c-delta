namespace CDelta.Ir;

public readonly record struct Terminator(
    TermKind Kind,
    OperandId Operand,
    BlockId Then,
    BlockId Else,
    IdRange Cases,
    UnreachableMode Mode,
    SourceSpan Span);
