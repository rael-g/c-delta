namespace CDelta.Ir;

public readonly record struct LocalDecl(StringId Name, TypeId Type, LocalKind Kind, SourceSpan Span);
