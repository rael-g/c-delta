namespace CDelta.Frontend.CSharp;

public sealed record ImportDeclaration(
    string Namespace,
    string ClassName,
    string SourcePath,
    IReadOnlyList<string> Headers,
    IReadOnlyList<string> Defines,
    IReadOnlyList<string> IncludeDirs,
    IReadOnlyList<string> Only);
