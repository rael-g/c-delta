static unsafe class M
{
    static void F(byte* ok, byte* bad)
    {
    }

    static void G()
    {
        F("ok", "\uD800");
    }
}
