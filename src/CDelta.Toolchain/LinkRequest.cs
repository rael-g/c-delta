namespace CDelta.Toolchain;

public readonly record struct LinkRequest(
    IReadOnlyList<string> Objects,
    string Output,
    string Target,
    OptimizeMode Mode,
    IReadOnlyList<string> Libraries);
