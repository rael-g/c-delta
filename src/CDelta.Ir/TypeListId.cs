namespace CDelta.Ir;

public readonly record struct TypeListId(int Index)
{
    public static readonly TypeListId None = new(-1);

    public bool IsNone => Index < 0;
}
