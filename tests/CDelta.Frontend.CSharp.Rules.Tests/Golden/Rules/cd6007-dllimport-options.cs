static unsafe class C
{
    [DllImport("c", CharSet = CharSet.Ansi)]
    static extern int a(byte* p);

    [DllImport("c", SetLastError = true)]
    static extern int b(byte* p);

    [DllImport("c", PreserveSig = false)]
    static extern int c(byte* p);

    [DllImport("c", EntryPoint = "puts")]
    static extern int d(byte* p);
}
