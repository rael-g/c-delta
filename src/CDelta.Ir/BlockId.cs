namespace CDelta.Ir;

public readonly record struct BlockId(int Index)
{
    public static readonly BlockId None = new(-1);

    public bool IsNone => Index < 0;
}
