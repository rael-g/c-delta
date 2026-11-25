namespace CDelta.Ir;

public readonly record struct TypeId(int Index)
{
    public static readonly TypeId None = new(-1);

    public bool IsNone => Index < 0;
}
