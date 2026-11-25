namespace CDelta.Toolchain;

internal static class IncludeProbe
{
    private const string StartMarker = "#include <...> search starts here:";
    private const string EndMarker = "End of search list.";
    private const string FrameworkSuffix = " (framework directory)";

    public static IReadOnlyList<string> Directories(IProcessRunner runner, string executable, IReadOnlyList<string> prefix)
    {
        string empty = Path.Combine(Path.GetTempPath(), "cdelta-empty-" + Guid.NewGuid().ToString("N") + ".c");
        File.WriteAllText(empty, string.Empty);
        try
        {
            var arguments = new List<string>(prefix) { "-E", "-x", "c", "-v", empty };
            ToolResult result = runner.Run(executable, arguments);
            return Parse(result.StandardError);
        }
        finally
        {
            File.Delete(empty);
        }
    }

    internal static IReadOnlyList<string> Parse(string output)
    {
        var directories = new List<string>();
        bool inside = false;
        foreach (string raw in output.Split('\n'))
        {
            string line = raw.TrimEnd('\r');
            if (line.StartsWith(StartMarker, StringComparison.Ordinal))
            {
                inside = true;
            }
            else if (line.StartsWith(EndMarker, StringComparison.Ordinal))
            {
                break;
            }
            else if (inside && line.StartsWith(' '))
            {
                directories.Add(line.Trim().Replace(FrameworkSuffix, string.Empty, StringComparison.Ordinal));
            }
        }

        return directories;
    }
}
