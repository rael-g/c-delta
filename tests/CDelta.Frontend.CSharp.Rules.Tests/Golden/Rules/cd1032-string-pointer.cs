static unsafe class M
{
    public const string Constant = "ok";

    static void F(string runtime)
    {
        byte* fine = "literal";
        byte* alsoFine = Constant;
        byte* bad = runtime;
    }
}
