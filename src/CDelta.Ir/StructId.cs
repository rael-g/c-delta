namespace CDelta.Ir;

public readonly record struct StructId(int Index)
{
    public static readonly StructId None = new(-1);

    public bool IsNone => Index < 0;
}
