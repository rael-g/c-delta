namespace CDelta;

using System;

public static class Intrinsics
{
    public static void Defer(Action body) => throw null;

    public static T Try<T, E>(Result<T, E> r) where E : struct, Enum => throw null;

    public static T Try<T>(Result<T> r) => throw null;

    public static void Try(Result r) => throw null;

    public static Exception Panic(ReadOnlySpan<byte> message) => throw null;
}
