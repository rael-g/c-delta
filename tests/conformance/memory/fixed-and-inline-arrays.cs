[InlineArray(4)]
struct Four
{
    public int Element;
}

unsafe struct Packet
{
    public fixed byte Name[8];
    public int Id;
}

static unsafe class Program
{
    public static readonly int[] Table = [1, 2, 3];

    static int Main()
    {
        Packet packet = default;
        packet.Name[2] = 7;
        int sum = packet.Name[2];
        Four four = default;
        four[1] = 5;
        sum += four[1];
        fixed (int* table = Table)
        {
            sum += table[2];
        }

        Span<int> values = [9, 8];
        fixed (int* pinned = values)
        {
            sum += pinned[1];
        }

        return sum;
    }
}
