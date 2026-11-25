namespace CDelta.Ir;

public readonly record struct ConstId(int Index)
{
    public static readonly ConstId None = new(-1);

    public bool IsNone => Index < 0;
}
