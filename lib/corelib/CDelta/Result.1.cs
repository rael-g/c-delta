namespace CDelta;

using System;

public struct Result<T>
{
    private T _value;
    private ushort _code;

    public static implicit operator Result<T>(T value) => throw null;

    public static implicit operator Result<T>(Error error) => throw null;
}
