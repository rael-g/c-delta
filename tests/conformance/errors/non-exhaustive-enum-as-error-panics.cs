[NonExhaustive]
enum Code { Good, Bad }

static class Program
{
    static Result Fail(Code code)
    {
        return Fail2(code);
    }

    static Result Fail2(Code code)
    {
        return CDelta.Intrinsics.Fail(code);
    }

    static int Main()
    {
        Result r = Fail((Code)9);
        return r.IsError ? 1 : 0;
    }
}
