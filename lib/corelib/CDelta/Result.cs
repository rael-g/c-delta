namespace CDelta;

using System;

public struct Result
{
    private ushort _code;

    public static implicit operator Result(Error error) => throw null;
}
