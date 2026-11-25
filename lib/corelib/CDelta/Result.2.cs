namespace CDelta;

using System;

public struct Result<T, E> where E : struct, Enum
{
    private T _value;
    private ushort _code;

    public bool IsError => throw null;

    public T Value => throw null;

    public E Error => throw null;

    public bool TryGet(out T value, out E error) => throw null;

    public static implicit operator Result<T, E>(T value) => throw null;

    public static implicit operator Result<T, E>(E error) => throw null;
}
