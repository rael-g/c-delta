namespace CDelta;

public static class Dyn<I>
{
    public static I Of<T>(ref T value) where T : I => throw null;
}
