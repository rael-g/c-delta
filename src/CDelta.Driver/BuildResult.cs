namespace CDelta.Driver;

public sealed record BuildResult(int ExitCode, string? OutputPath, IReadOnlyList<string> Messages)
{
    public IReadOnlyList<string> GeneratedC { get; init; } = [];
}
