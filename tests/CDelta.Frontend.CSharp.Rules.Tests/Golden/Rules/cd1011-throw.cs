static class M
{
    static void F()
    {
        throw new Exception();
    }

    static int G()
    {
        throw Panic("x");
    }
}
