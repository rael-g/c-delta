static class M
{
    [ThreadStatic] public static int Good;
}
struct S
{
    [ThreadStatic] public int Bad;
}
