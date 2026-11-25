namespace CDelta.Frontend.C;

public sealed record ReadOptions(
    string Target,
    IReadOnlyList<string> SystemIncludeDirs,
    IReadOnlyList<string> IncludeDirs,
    IReadOnlyList<string> Defines);
