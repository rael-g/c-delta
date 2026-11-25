namespace CDelta.Ir;

public readonly record struct StatementId(int Index)
{
    public static readonly StatementId None = new(-1);

    public bool IsNone => Index < 0;
}
