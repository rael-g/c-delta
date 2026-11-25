namespace CDelta.Ir;

public readonly record struct BytesId(int Index)
{
    public static readonly BytesId None = new(-1);

    public bool IsNone => Index < 0;
}
