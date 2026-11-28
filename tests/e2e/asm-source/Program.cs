[Import("seven.h")]
static unsafe partial class N { }

static unsafe class Program
{
    static int Main()
    {
        return N.asm_seven() * 6;
    }
}
