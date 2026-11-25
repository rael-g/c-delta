[Import("stdio.h")]
static unsafe partial class C { }

static unsafe class Program
{
    static int Main()
    {
        C.puts("hello from c");
        return 3;
    }
}
