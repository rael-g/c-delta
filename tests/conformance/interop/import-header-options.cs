[Import("answer.h", IncludeDirs = ["headers"], Defines = ["ANSWER_BASE=40"], Only = ["answer", "ANSWER", "advance", "counter"])]
static unsafe partial class A { }

static unsafe class Program
{
    static int Main()
    {
        var counter = new A.counter { total = 1, step = 2 };
        A.advance(&counter);
        return A.answer() + A.ANSWER + counter.total;
    }
}
