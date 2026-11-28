static class G
{
    [Export("same")] public static int First() { return 1; }

    [Export("same")] public static int Second() { return 2; }

    [Export] public static int Third() { return 3; }

    [Export("Third")] public static int Fourth() { return 4; }
}
