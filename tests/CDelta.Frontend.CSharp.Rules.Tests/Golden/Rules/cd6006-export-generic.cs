static class G
{
    [Export] public static T Identity<T>(T value) { return value; }

    [Export] public static int Plain(int value) { return value; }
}
