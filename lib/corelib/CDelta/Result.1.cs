namespace CDelta;

using System;

public struct Result<T>
{
    private T _value;
    private ushort _code;

    public bool IsError => throw null;

    public T Value => throw null;

    public Error Error => throw null;

    public bool TryGet(out T value, out Error error) => throw null;

    public static implicit operator Result<T>(T value) => throw null;

    public static implicit operator Result<T>(Error error) => throw null;
}
