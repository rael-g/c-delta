namespace CDelta.Ir;

public readonly record struct FieldDecl(StringId Name, TypeId Type, IdRange Attributes, int BitWidth, int Offset)
{
    public const int NoOffset = -1;

    public const int NoBits = 0;
}
