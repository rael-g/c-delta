static unsafe class Program
{
    [DllImport("c", EntryPoint = "puts")]
    static extern int PutsText(byte* text);

    [DllImport("c", EntryPoint = "puts")]
    static extern int PutsNumber(int value);

    static int Main()
    {
        PutsText("a");
        return PutsNumber(1);
    }
}
