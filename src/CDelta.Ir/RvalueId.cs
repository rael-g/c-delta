namespace CDelta.Ir;

public readonly record struct RvalueId(int Index)
{
    public static readonly RvalueId None = new(-1);

    public bool IsNone => Index < 0;
}
