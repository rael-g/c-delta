namespace CDelta.Driver;

public sealed record ProjectPlan(
    string ProjectPath,
    string PackageName,
    string? Target,
    string? OptimizeMode,
    string? Toolchain,
    bool EmitC,
    IReadOnlyList<string> Sources,
    IReadOnlyList<(string Path, string Flags)> CSources,
    IReadOnlyList<(string Path, string Flags)> CppSources,
    IReadOnlyList<string> IncludeDirectories,
    IReadOnlyList<string> Defines,
    IReadOnlyList<(string Name, bool Static)> Libraries,
    IReadOnlyList<string> LibraryDirectories,
    IReadOnlyList<(string Path, string Flags)> AsmSources,
    string? EntrySymbol,
    string? LinkerScript,
    IReadOnlyList<string> LinkerArgs,
    string? OutputType = null,
    string? LibraryKind = null)
{
    public string Directory => System.IO.Path.GetDirectoryName(ProjectPath)!;
}
