namespace CDelta.Ir;

public readonly record struct InterfaceId(int Index)
{
    public static readonly InterfaceId None = new(-1);

    public bool IsNone => Index < 0;
}
