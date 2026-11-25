[StructLayout(LayoutKind.Explicit)]
struct Number
{
    [FieldOffset(0)] public int Whole;
    [FieldOffset(0)] public uint Bits;
    [FieldOffset(4)] public short Extra;
}

[StructLayout(LayoutKind.Explicit, Size = 16)]
struct Padded
{
    [FieldOffset(2)] public int Misplaced;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
struct Packed
{
    public byte Tag;
    public int Value;
}

static unsafe class Program
{
    static int Main()
    {
        Number n;
        n.Bits = 0;
        n.Extra = 3;
        n.Whole = -1;
        Padded p;
        p.Misplaced = 40;
        Packed k;
        k.Tag = 1;
        k.Value = 100;
        return (int)(n.Bits >> 28) + n.Extra + p.Misplaced + k.Tag + k.Value + sizeof(Number) + sizeof(Padded) + sizeof(Packed);
    }
}
