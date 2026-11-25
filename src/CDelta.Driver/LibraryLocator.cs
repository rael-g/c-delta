namespace CDelta.Driver;

public static class LibraryLocator
{
    private const string LibraryVariable = "CDELTA_LIB";
    private const string CorelibDirectory = "corelib";
    private const string StdDirectory = "std";
    private const string CorelibFile = "CDelta.Corelib.dll";

    public static IReadOnlyList<string> StdSources(Func<string, string?> environment)
    {
        string? root = environment(LibraryVariable);
        string directory = Path.Combine(root ?? Path.Combine(AppContext.BaseDirectory, "lib"), StdDirectory);
        return Directory.Exists(directory)
            ? [.. Directory.GetFiles(directory, "*.cs", SearchOption.AllDirectories).Order(StringComparer.Ordinal)]
            : [];
    }

    public static string Corelib(Func<string, string?> environment)
    {
        string? root = environment(LibraryVariable);
        string libDirectory = root ?? Path.Combine(AppContext.BaseDirectory, "lib");
        return Path.Combine(libDirectory, CorelibDirectory, CorelibFile);
    }
}
