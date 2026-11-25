static class Math2
{
    static int Combine(int a, int b, long c)
    {
        int sum = a + b * 2;
        int wrapped = unchecked(a * b);
        int trapped = checked(a - b);
        long wide = c + a;
        int narrow = (int)c;
        int neg = -a;
        bool flag = a < b;
        bool both = a < b && b < 10;
        return sum + wrapped + trapped + (int)wide + narrow + neg + (flag && both ? 1 : 0) + a / b + a % b + (a << 2) + (a & b) + (~a);
    }

    static int Main()
    {
        return Combine(1, 2, 3L);
    }
}
