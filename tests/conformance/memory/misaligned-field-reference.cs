[StructLayout(LayoutKind.Explicit)]
struct Odd
{
    [FieldOffset(2)] public int Value;
}

static class Program
{
    static void Touch(ref int value)
    {
        value = 1;
    }

    static void Main()
    {
        Odd odd;
        odd.Value = 5;
        Touch(ref odd.Value);
    }
}
