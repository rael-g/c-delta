static class Geometry
{
    [Export("geo_distance2")]
    public static int Distance2(int ax, int ay, int bx, int by) => (ax - bx) * (ax - bx) + (ay - by) * (ay - by);

    [Export]
    public static int Clamp(int value, int low, int high) => value < low ? low : value > high ? high : value;

    static int Hidden(int value) => value * 2;

    [Export("geo_twice")]
    public static int Twice(int value) => Hidden(value);
}
