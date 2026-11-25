namespace System;

public readonly struct Range
{
    private readonly Index _start;
    private readonly Index _end;

    public Range(Index start, Index end)
    {
        _start = start;
        _end = end;
    }

    public Index Start => _start;

    public Index End => _end;
}
