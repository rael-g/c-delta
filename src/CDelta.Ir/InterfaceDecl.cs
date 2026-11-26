namespace CDelta.Ir;

public readonly record struct InterfaceDecl(StringId Key, StringId Name, SourceSpan Span, IdRange TypeParams, IdRange Methods);
