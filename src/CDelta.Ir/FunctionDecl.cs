namespace CDelta.Ir;

public readonly record struct FunctionDecl(
    StringId Key,
    StringId Name,
    SourceSpan Span,
    Origin Origin,
    IdRange Attributes,
    IdRange Params,
    bool Variadic,
    TypeId Return,
    ParamMode ReturnMode,
    CallConv Conv,
    LinkageKind Linkage,
    StringId ExportName,
    bool ExportWeak,
    InlineKind Inline,
    SafetyOverride Safety,
    IdRange TypeParams = default);
