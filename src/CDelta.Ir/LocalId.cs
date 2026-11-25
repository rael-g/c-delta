namespace CDelta.Ir;

public readonly record struct LocalId(int Index)
{
    public static readonly LocalId None = new(-1);

    public bool IsNone => Index < 0;
}
