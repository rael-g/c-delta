static class Program
{
    static int? Find(int target)
    {
        if (target > 2)
        {
            return target * 10;
        }

        return null;
    }

    static int Main()
    {
        int? present = Find(3);
        int? missing = Find(1);
        int total = 0;
        if (present.HasValue)
        {
            total += present.Value;
        }

        if (!missing.HasValue)
        {
            total += 1;
        }

        total += missing.GetValueOrDefault(5);
        total += present.GetValueOrDefault();
        return total;
    }
}
