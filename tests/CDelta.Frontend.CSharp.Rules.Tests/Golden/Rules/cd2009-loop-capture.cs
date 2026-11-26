delegate int Getter();

static class M
{
    public static Getter stored;

    static void F(System.ReadOnlySpan<int> items)
    {
        Getter outside = null;
        foreach (int item in items)
        {
            int local = item + 1;
            Getter inside = () => local;
            outside = () => item;
            stored = inside;
            stored = () => local;
            int use = inside();
        }

        for (int i = 0; i < 3; i++)
        {
            outside = () => i;
        }

        int shared = 0;
        while (shared < 2)
        {
            outside = () => shared;
            shared++;
        }
    }
}
