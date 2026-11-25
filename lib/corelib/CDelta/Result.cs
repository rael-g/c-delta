namespace CDelta;

using System;

public struct Result
{
    private ushort _code;

    public bool IsError => throw null;

    public Error Error => throw null;

    public static implicit operator Result(Error error) => throw null;
}
