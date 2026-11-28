[Export]
struct Point { public int X; public int Y; }

struct Hidden { public int Z; }

static unsafe class G
{
    [Export] public static int Fine(Point p, int n, byte* b, delegate*<int, int> f) { return n; }

    [Export] public static string Wide(int n) { return default; }

    [Export] public static int Spans(System.ReadOnlySpan<byte> bytes) { return bytes.Length; }

    [Export] public static Hidden Unexported() { return default; }
}
