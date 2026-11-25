enum FileError { NotFound, AccessDenied, IsDirectory }

static class Program
{
    static Result<int, FileError> Open(int id)
    {
        if (id == 0) return FileError.NotFound;
        if (id == 1) return FileError.AccessDenied;
        if (id == 2) return FileError.IsDirectory;
        return id * 10;
    }

    static int Classify(int id)
    {
        if (Open(id).TryGet(out int value, out FileError error))
        {
            return value;
        }

        switch (error)
        {
            case FileError.NotFound: return 1;
            case FileError.AccessDenied: return 2;
            default: return 3;
        }
    }

    static int Main()
    {
        int total = Classify(0) + Classify(1) + Classify(2) + Classify(5);
        Result<int, FileError> r = Open(1);
        if (r.IsError && r.Error == FileError.AccessDenied) total += 100;
        total += Open(4).Value;
        return total;
    }
}
