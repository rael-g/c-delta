using Std;

[Import("stdio.h")]
static unsafe partial class C { }

enum CountError { CannotOpen, ReadFailed }

static unsafe class Program
{
    static Result<long, CountError> CountLines(byte* path)
    {
        var file = C.fopen(path, "rb");
        if (file == null) return CountError.CannotOpen;
        Defer(() => C.fclose(file));

        long lines = 0;
        int ch;
        while ((ch = C.fgetc(file)) != C.EOF)
            if (ch == '\n') lines++;
        if (C.ferror(file) != 0) return CountError.ReadFailed;
        return lines;
    }

    static Result Main()
    {
        var args = Process.Args();
        if (args.Length != 2)
        {
            Debug.Print("usage: count-lines <file>\n");
            Process.Exit(2);
        }
        var lines = Try(CountLines(args[1].Pointer));
        C.printf("%lld\n", __arglist(lines));
        return default;
    }
}
