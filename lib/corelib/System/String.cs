namespace System;

public sealed class String
{
    public static unsafe implicit operator byte*(string value) => throw null;

    public static implicit operator ReadOnlySpan<byte>(string value) => throw null;
}
