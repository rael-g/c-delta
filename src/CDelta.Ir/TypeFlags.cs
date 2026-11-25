namespace CDelta.Ir;

[Flags]
public enum TypeFlags
{
    None = 0,
    Nullable = 1,
    Const = 2,
    Volatile = 4,
    Many = 8,
}
