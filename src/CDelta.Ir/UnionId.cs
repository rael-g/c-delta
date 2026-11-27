namespace CDelta.Ir;

public readonly record struct UnionId(int Index)
{
    public static readonly UnionId None = new(-1);

    public bool IsNone => Index < 0;
}
