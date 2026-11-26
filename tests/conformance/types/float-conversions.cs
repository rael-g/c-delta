static class Program
{
    static double Scale(int value, double factor)
    {
        return value * factor;
    }

    static int Main()
    {
        double doubled = Scale(7, 0.5) * 2.0;
        float narrow = (float)doubled;
        int truncated = (int)2.9;
        int negative = (int)-2.9;
        long wide = (long)(narrow + 0.25f);
        double nan = 0.0 / 0.0;
        int saturated = unchecked((int)nan) + unchecked((int)1e20) % 7;
        return (int)doubled + truncated * 10 + negative + (int)wide + saturated * 0 + (nan != nan ? 100 : 0);
    }
}
