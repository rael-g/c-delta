static class Tables
{
    public static readonly int[] Primes = [2, 3, 5, 7, 11];
    public static readonly byte[] Bytes = new byte[] { 1, 2, 3 };
}

static class Program
{
    static int Main()
    {
        int sum = 0;
        for (int i = 0; i < Tables.Primes.Length; i++)
        {
            sum += Tables.Primes[i];
        }

        Tables.Primes[0] = 10;
        return sum + Tables.Primes[0] + Tables.Bytes[2] + Tables.Bytes.Length;
    }
}
