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
    IReadOnlyList<string> IncludeDirectories,
    IReadOnlyList<string> Defines,
    IReadOnlyList<(string Name, bool Static)> Libraries,
    IReadOnlyList<string> LibraryDirectories)
{
    public string Directory => System.IO.Path.GetDirectoryName(ProjectPath)!;
}
