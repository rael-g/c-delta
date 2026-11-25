[Import("stdio.h")]
static unsafe partial class C { }

static unsafe class Program
{
    static int Main()
    {
        var file = C.fopen("/proc/self/stat", "rb");
        if (file == null) return 1;
        Defer(() => C.fclose(file));

        int count = 0;
        int ch;
        while ((ch = C.fgetc(file)) != C.EOF)
        {
            count++;
            if (count > 3) break;
        }

        C.printf("read %d bytes\n", __arglist(count));
        return C.ferror(file) == 0 ? 0 : 2;
    }
}
