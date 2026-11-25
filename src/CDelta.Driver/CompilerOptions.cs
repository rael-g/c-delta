namespace CDelta.Driver;

public sealed record CompilerOptions(
    IReadOnlyList<string> Files,
    string Target,
    string Mode,
    string? OutputPath,
    string Toolchain,
    bool EmitC,
    string? DumpIrStage,
    bool Verbose,
    bool Time);
