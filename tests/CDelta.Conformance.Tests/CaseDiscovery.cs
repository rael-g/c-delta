using CDelta.Driver;
using CDelta.Tests;

namespace CDelta.Conformance.Tests;

internal static class CaseDiscovery
{
    private static readonly string[] _allModes = ["Debug", "ReleaseSafe", "ReleaseFast", "ReleaseSmall"];
    private static readonly string[] _safeModes = ["Debug", "ReleaseSafe"];

    public static string Root() => Path.Combine(GoldenFile.RepositoryRoot(), "tests", "conformance");

    public static IEnumerable<ConformanceCase> Cases()
    {
        string? modeFilter = Environment.GetEnvironmentVariable("CDELTA_MODE");
        string? targetFilter = Environment.GetEnvironmentVariable("CDELTA_TARGET");
        string? textFilter = Environment.GetEnvironmentVariable("CDELTA_FILTER");
        string host = HostTarget.Resolve() ?? "x86_64-linux-gnu";
        string root = Root();
        foreach (string path in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
        {
            string relative = Path.GetRelativePath(root, path).Replace('\\', '/');
            if (textFilter is not null && !relative.Contains(textFilter, StringComparison.Ordinal))
            {
                continue;
            }

            string stem = path[..^".cs".Length];
            string[] modes = ReadList(stem + ".modes") ?? (File.Exists(stem + ".panic") ? _safeModes : _allModes);
            string[] targets = ReadList(stem + ".targets") ?? [host];
            foreach (string mode in modes)
            {
                foreach (string target in targets)
                {
                    if ((modeFilter is null || modeFilter == mode) && (targetFilter is null || targetFilter == target))
                    {
                        yield return new ConformanceCase(relative, mode, target == "native" ? host : target);
                    }
                }
            }
        }
    }

    private static string[]? ReadList(string path) =>
        File.Exists(path)
            ? File.ReadAllText(path).Split([' ', '\n', '\r', '\t'], StringSplitOptions.RemoveEmptyEntries)
            : null;
}
