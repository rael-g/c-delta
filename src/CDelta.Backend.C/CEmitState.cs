namespace CDelta.Backend.C;

internal sealed class CEmitState
{
    public bool UsesPanic { get; set; }

    public bool UsesUnusedMarker { get; set; }

    public bool UsesAlloca { get; set; }

    public bool UsesTrace { get; set; }

    public bool UsesReport { get; set; }

    public bool UsesTextEql { get; set; }

    public bool UsesUtf8Next { get; set; }

    public bool UsesTracePush { get; set; }

    public SortedSet<(string From, string To, bool Trap)> FloatCasts { get; } = [];

    public SortedSet<int> ErrorFromEnums { get; } = [];

    public SortedSet<int> ErrorToEnums { get; } = [];
}
