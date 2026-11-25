namespace CDelta.Backend.C;

internal sealed class CEmitState
{
    public bool UsesPanic { get; set; }

    public bool UsesUnusedMarker { get; set; }

    public bool UsesAlloca { get; set; }
}
