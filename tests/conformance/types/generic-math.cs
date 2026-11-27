using System.Numerics;

struct Vector2 : IAdditionOperators<Vector2, Vector2, Vector2>, IMultiplyOperators<Vector2, Vector2, Vector2>,
    ISubtractionOperators<Vector2, Vector2, Vector2>
{
    public int X;
    public int Y;

    public static Vector2 operator +(Vector2 a, Vector2 b) => new() { X = a.X + b.X, Y = a.Y + b.Y };

    public static Vector2 operator -(Vector2 a, Vector2 b) => new() { X = a.X - b.X, Y = a.Y - b.Y };

    public static Vector2 operator *(Vector2 a, Vector2 b) => new() { X = a.X * b.X, Y = a.Y * b.Y };
}

static class P
{
    static T Sum<T>(T a, T b, T c) where T : INumber<T> => a + b + c;

    static T Lerp<T>(T a, T b, T t) where T : INumber<T> => a + (b - a) * t;

    static T Origin<T>() where T : INumber<T> => T.Zero;

    static T Mix<T>(T a, T b, T c) where T : IAdditionOperators<T, T, T>, ISubtractionOperators<T, T, T>, IMultiplyOperators<T, T, T> => a + (b - a) * c;

    static T Larger<T>(T a, T b) where T : INumber<T> => a > b ? a : b;

    public static int Main()
    {
        int i = Sum(10, 20, 4);
        float f = Lerp(0f, 10f, 0.5f);
        double d = Lerp(2.0, 4.0, 1.0) + Origin<double>() + 1.0;
        long l = Larger(3L, 5L) + Origin<long>();
        Vector2 a = default;
        a.X = 1;
        Vector2 b = default;
        b.X = 3;
        Vector2 t = default;
        t.X = 2;
        Vector2 m = Mix(a, b, t);
        return i + (int)f + (int)d + (int)l + m.X - 12;
    }
}
