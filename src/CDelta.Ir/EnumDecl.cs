namespace CDelta.Ir;

public readonly record struct EnumDecl(
    StringId Key,
    StringId Name,
    SourceSpan Span,
    Origin Origin,
    IdRange Attributes,
    TypeId Underlying,
    bool Exhaustive,
    IdRange Members);
