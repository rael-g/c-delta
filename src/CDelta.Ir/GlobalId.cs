namespace CDelta.Ir;

public readonly record struct GlobalId(int Index)
{
    public static readonly GlobalId None = new(-1);

    public bool IsNone => Index < 0;
}
