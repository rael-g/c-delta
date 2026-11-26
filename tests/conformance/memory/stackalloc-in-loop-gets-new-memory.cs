static unsafe class Program
{
    static int Main()
    {
        int* first = null;
        int* second = null;
        for (int i = 0; i < 2; i++)
        {
            int* block = stackalloc int[2] { i + 1, i + 2 };
            if (i == 0) first = block;
            else second = block;
        }

        return first[0] * 10 + second[0] + (first == second ? 100 : 0);
    }
}
