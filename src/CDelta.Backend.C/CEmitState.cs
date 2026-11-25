namespace CDelta.Backend.C;

internal sealed class CEmitState
{
    public bool UsesPanic { get; set; }

    public bool UsesUnusedMarker { get; set; }

    public bool UsesAlloca { get; set; }

    public bool UsesTrace { get; set; }

    public SortedSet<int> ErrorFromEnums { get; } = [];

    public SortedSet<int> ErrorToEnums { get; } = [];
}
