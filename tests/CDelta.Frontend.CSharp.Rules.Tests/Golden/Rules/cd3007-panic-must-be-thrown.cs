static class P
{
    static int Run(bool flag)
    {
        var e = Panic("x"u8);
        if (flag) throw Unreachable();
        throw Panic("y"u8);
    }
}
