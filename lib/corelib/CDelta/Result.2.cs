namespace CDelta;

using System;

public struct Result<T, E> where E : struct, Enum
{
    private T _value;
    private ushort _code;

    public static implicit operator Result<T, E>(T value) => throw null;

    public static implicit operator Result<T, E>(E error) => throw null;
}
