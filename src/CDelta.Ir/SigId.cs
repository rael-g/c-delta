namespace CDelta.Ir;

public readonly record struct SigId(int Index)
{
    public static readonly SigId None = new(-1);

    public bool IsNone => Index < 0;
}
