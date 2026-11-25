namespace CDelta.Ir;

public readonly record struct SigParam(TypeId Type, ParamMode Mode)
{
    private const int ModeBits = 2;
    private const int ModeMask = (1 << ModeBits) - 1;

    public int Encode() => (Type.Index << ModeBits) | (int)Mode;

    public static SigParam Decode(int value) => new(new TypeId(value >> ModeBits), (ParamMode)(value & ModeMask));
}
