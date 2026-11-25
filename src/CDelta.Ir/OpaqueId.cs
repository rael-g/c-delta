namespace CDelta.Ir;

public readonly record struct OpaqueId(int Index)
{
    public static readonly OpaqueId None = new(-1);

    public bool IsNone => Index < 0;
}
