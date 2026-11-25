namespace CDelta.Ir;

public readonly record struct OperandId(int Index)
{
    public static readonly OperandId None = new(-1);

    public bool IsNone => Index < 0;
}
