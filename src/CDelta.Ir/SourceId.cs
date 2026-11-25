namespace CDelta.Ir;

public readonly record struct SourceId(int Index)
{
    public static readonly SourceId None = new(-1);

    public bool IsNone => Index < 0;
}
