[Import("sum.h")]
static unsafe partial class N { }

static unsafe class Program
{
    static int Main()
    {
        return N.sum_squares(4);
    }
}
