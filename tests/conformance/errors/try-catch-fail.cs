enum ParseError { Empty, InvalidDigit }
enum RangeError { Empty, TooBig }

static class Program
{
    static Result<int, ParseError> Digit(int c)
    {
        if (c < 0) return ParseError.Empty;
        if (c > 9) return ParseError.InvalidDigit;
        return c;
    }

    static Result<int, ParseError> Twice(int c)
    {
        int d = Try(Digit(c));
        return d * 2;
    }

    static Result<int> Any(int c)
    {
        if (c == 99) return Fail(RangeError.TooBig);
        int d = Try(Twice(c));
        return d + 1;
    }

    static int Main()
    {
        int total = Catch(Twice(4), 100);
        total += Catch(Twice(12), 7);
        total += Catch(Any(3), 1000);
        total += Catch(Any(99), 20);
        return total;
    }
}
