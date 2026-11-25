using System.Security.Cryptography;
using System.Text;

namespace CDelta.Driver;

internal static class BuildPaths
{
    private const string CacheName = "cdelta";
    private const string FilesDirectory = "files";
    private const int IdLength = 16;

    public static string CacheRoot(Func<string, string?> environment)
    {
        if (OperatingSystem.IsWindows())
        {
            return Path.Combine(environment("LOCALAPPDATA") ?? Path.GetTempPath(), CacheName);
        }

        string? xdg = environment("XDG_CACHE_HOME");
        if (!string.IsNullOrEmpty(xdg))
        {
            return Path.Combine(xdg, CacheName);
        }

        return Path.Combine(environment("HOME") ?? Path.GetTempPath(), ".cache", CacheName);
    }

    public static string IntermediateForFiles(Func<string, string?> environment, IReadOnlyList<string> fullPaths)
    {
        var sorted = new List<string>(fullPaths);
        sorted.Sort(StringComparer.Ordinal);
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\n', sorted)));
        string id = Convert.ToHexString(hash).ToLowerInvariant()[..IdLength];
        return Path.Combine(CacheRoot(environment), FilesDirectory, id);
    }
}
