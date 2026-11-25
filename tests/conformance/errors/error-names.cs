enum FileError { NotFound, AccessDenied }

static class Program
{
    static Result Fails() { return Fail(FileError.AccessDenied); }

    static int Main()
    {
        Error e = Fails().Error;
        ReadOnlySpan<byte> name = e.Name;
        return name.Length * 10 + name[0];
    }
}
