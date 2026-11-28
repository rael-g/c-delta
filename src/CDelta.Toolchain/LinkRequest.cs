namespace CDelta.Toolchain;

public readonly record struct LinkRequest(
    IReadOnlyList<string> Objects,
    string Output,
    string Target,
    OptimizeMode Mode,
    IReadOnlyList<string> Libraries,
    IReadOnlyList<NativeLibrary>? NativeLibraries = null,
    IReadOnlyList<string>? LibraryDirectories = null,
    bool Cpp = false);
