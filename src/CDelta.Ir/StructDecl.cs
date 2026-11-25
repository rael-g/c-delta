namespace CDelta.Ir;

public readonly record struct StructDecl(
    StringId Key,
    StringId Name,
    SourceSpan Span,
    Origin Origin,
    IdRange Attributes,
    IdRange Fields,
    StructLayoutKind Layout,
    int Pack,
    int MinSize,
    int Size,
    int Alignment)
{
    public const int NotComputed = -1;
}
