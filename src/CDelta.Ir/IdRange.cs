namespace CDelta.Ir;

public readonly record struct IdRange(int Start, int Count)
{
    public static readonly IdRange Empty = new(0, 0);
}
