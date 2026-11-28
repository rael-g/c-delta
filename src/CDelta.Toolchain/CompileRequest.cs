namespace CDelta.Toolchain;

public readonly record struct CompileRequest(
    string Source,
    string Output,
    string Target,
    OptimizeMode Mode,
    IReadOnlyList<string> IncludeDirectories,
    IReadOnlyList<string> Defines,
    IReadOnlyList<string>? ExtraFlags = null,
    bool Cpp = false,
    bool Assembly = false,
    bool SharedLibrary = false);
