interface ICounter
{
    int Get();

    void Bump();

    int BumpTwice()
    {
        Bump();
        Bump();
        return Get();
    }

    int Doubled() => Get() * 2;
}

struct Counter : ICounter
{
    public int N;

    public int Get() => N;

    public void Bump()
    {
        N++;
    }
}

struct Fixed : ICounter
{
    public int N;

    public int Get() => N;

    public void Bump()
    {
        N++;
    }

    public int Doubled() => 100;
}

[Union]
interface ISized
{
    int Size();

    int Triple() => Size() * 3;
}

struct Box : ISized
{
    public int Side;

    public int Size() => Side;
}

interface IScaled<T>
{
    T Value();

    int Weight() => 4;
}

struct Meter : IScaled<int>
{
    public int Reading;

    public int Value() => Reading;
}

static class P
{
    static int Through<T>(ref T counter) where T : ICounter => counter.BumpTwice() * 10 + counter.Get();

    static int Twice<T>(ref T counter) where T : ICounter => counter.Doubled();

    static int Weigh<T>(ref T meter) where T : IScaled<int> => meter.Weight();

    public static int Main()
    {
        Counter counter = default;
        counter.N = 1;
        Fixed fixedCounter = default;
        fixedCounter.N = 2;
        Counter shared = default;
        shared.N = 5;
        ICounter dynamic = Dyn<ICounter>.Of(ref shared);
        Box box = default;
        box.Side = 2;
        ISized sized = box;
        Meter meter = default;
        meter.Reading = 6;
        return Through(ref counter) + Twice(ref fixedCounter) + Twice(ref counter) + dynamic.BumpTwice() + dynamic.Doubled() + sized.Triple() + Weigh(ref meter);
    }
}
