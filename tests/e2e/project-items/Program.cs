[Import("add.h")]
static unsafe partial class N { }

[Import("math.h", Only = ["floor"])]
static unsafe partial class M { }

static unsafe class Program
{
    static int Main()
    {
        return N.add_scaled(1, 2) + (int)M.floor(2.5);
    }
}
