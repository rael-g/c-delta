namespace CDelta.Ir;

public readonly record struct SourceSpan(SourceId File, int Line, int Column)
{
    public static readonly SourceSpan None = new(SourceId.None, 0, 0);

    public bool IsNone => File.IsNone;
}
