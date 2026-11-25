namespace CDelta.Ir;

public readonly record struct EnumId(int Index)
{
    public static readonly EnumId None = new(-1);

    public bool IsNone => Index < 0;
}
