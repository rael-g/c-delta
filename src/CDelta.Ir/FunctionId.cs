namespace CDelta.Ir;

public readonly record struct FunctionId(int Index)
{
    public static readonly FunctionId None = new(-1);

    public bool IsNone => Index < 0;
}
