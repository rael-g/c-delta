namespace CDelta.Ir;

public sealed record CImportDecl(IReadOnlyList<string> Headers, IReadOnlyList<string> Defines, IReadOnlyList<string> IncludeDirs);
