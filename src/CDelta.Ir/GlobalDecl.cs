namespace CDelta.Ir;

public readonly record struct GlobalDecl(
    StringId Key,
    StringId Name,
    SourceSpan Span,
    Origin Origin,
    IdRange Attributes,
    TypeId Type,
    bool Mutable,
    bool ThreadLocal,
    ConstId Initializer,
    LinkageKind Linkage,
    StringId ExportName,
    StringId Section,
    int Alignment);
