static class M
{
    static void F(Action a, Action b)
    {
        var c = a + b;
        a += b;
    }
}
