namespace CDelta.Cli;

internal sealed record CommandLine(
    string Command,
    IReadOnlyList<string> Files,
    IReadOnlyList<string> ProgramArguments,
    string Target,
    string Mode,
    string? Output,
    string Toolchain,
    bool EmitC,
    string? DumpIr,
    bool Verbose,
    bool Time)
{
    public const string Usage =
        "usage: cdc <build|run> <files.cs> [--target t] [--mode m] [-o path] [--emit-c] [--dump-ir stage] "
        + "[--toolchain name] [--option name=value] [--verbose] [--time] [--color mode] [-- args]";
}
