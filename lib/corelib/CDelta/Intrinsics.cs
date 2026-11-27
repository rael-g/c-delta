namespace CDelta;

using System;

public static class Intrinsics
{
    public static void Defer(Action body) => throw null;

    public static T Try<T, E>(this Result<T, E> r) where E : struct, Enum => throw null;

    public static T Try<T>(this Result<T> r) => throw null;

    public static void Try(this Result r) => throw null;

    public static void ErrDefer(Action body) => throw null;

    public static void ErrDefer(Action<Error> body) => throw null;

    public static T Catch<T, E>(this Result<T, E> r, T fallback) where E : struct, Enum => throw null;

    public static T Catch<T>(this Result<T> r, T fallback) => throw null;

    public static Error Fail<E>(E error) where E : struct, Enum => throw null;

    public static Exception Unreachable() => throw null;

    public static Exception Panic(ReadOnlySpan<byte> message) => throw null;
}
