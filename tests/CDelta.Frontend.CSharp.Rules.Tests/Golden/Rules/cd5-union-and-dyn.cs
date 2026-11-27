[Union]
interface IShape
{
}

interface ICounter
{
}

struct Circle : IShape
{
}

struct Generic<T> : IShape
{
}

ref struct Stack : IShape
{
}

struct Meter : ICounter
{
}

static class Program
{
    static int Union(IShape shape) => shape is Circle ? 1 : 0;

    static int Closed(ICounter counter) => counter is Meter ? 1 : 0;

    static ICounter Box(Meter meter) => meter;

    static IShape Wrap(Circle circle) => circle;

    static IShape Erase(ref Circle circle) => Dyn<IShape>.Of(ref circle);
}
