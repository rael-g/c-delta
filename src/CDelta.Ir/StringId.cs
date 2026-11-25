namespace CDelta.Ir;

public readonly record struct StringId(int Index)
{
    public static readonly StringId None = new(-1);

    public bool IsNone => Index < 0;
}
