namespace CDelta.Ir;

public readonly record struct PlaceId(int Index)
{
    public static readonly PlaceId None = new(-1);

    public bool IsNone => Index < 0;
}
