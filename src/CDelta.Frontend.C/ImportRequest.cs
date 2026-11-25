namespace CDelta.Frontend.C;

public sealed record ImportRequest(
    IReadOnlyList<string> Headers,
    IReadOnlyList<string> Defines,
    IReadOnlyList<string> IncludeDirs,
    IReadOnlyList<string> Only);
