namespace CDelta.Ir;

public readonly record struct OpaqueDecl(StringId Key, StringId Name, SourceSpan Span, Origin Origin, IdRange Attributes);
