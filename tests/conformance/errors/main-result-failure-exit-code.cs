enum StartError { Missing }

static class Program
{
    static Result<int, StartError> Load() { return StartError.Missing; }

    static Result Main()
    {
        int value = Load().Try();
        return default;
    }
}
