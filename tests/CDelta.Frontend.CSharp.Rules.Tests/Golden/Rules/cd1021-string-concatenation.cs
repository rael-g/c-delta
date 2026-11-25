static unsafe class M
{
    public const string A = "a" + "b";

    static void F(byte* p)
    {
        byte* q = "a" + A;
        var s = "x";
        var t = s + "y";
        var u = $"value {1}";
    }
}
