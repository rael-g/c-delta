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
        int d = Digit(c).Try();
        return d * 2;
    }

    static Result<int> Any(int c)
    {
        if (c == 99) return Fail(RangeError.TooBig);
        int d = Twice(c).Try();
        return d + 1;
    }

    static int Main()
    {
        int total = Twice(4).Catch(100);
        total += Twice(12).Catch(7);
        total += Any(3).Catch(1000);
        total += Any(99).Catch(20);
        return total;
    }
}
