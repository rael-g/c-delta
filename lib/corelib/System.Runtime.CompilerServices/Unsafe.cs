namespace System.Runtime.CompilerServices;

public static unsafe class Unsafe
{
    public static ref TTo As<TFrom, TTo>(ref TFrom source) => throw null;

    public static int SizeOf<T>() => throw null;

    public static void SkipInit<T>(out T value) => throw null;

    public static TTo BitCast<TFrom, TTo>(TFrom source) => throw null;
}
