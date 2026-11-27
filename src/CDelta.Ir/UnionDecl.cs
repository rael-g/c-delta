namespace CDelta.Ir;

public readonly record struct UnionDecl(StringId Key, StringId Name, SourceSpan Span, TypeId Interface, IdRange Members, TypeId Tag, int Size, int Alignment)
{
    public const int NotComputed = -1;
}
