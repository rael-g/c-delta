namespace CDelta.Toolchain;

public static class PathSearch
{
    public static string? Find(string name)
    {
        string? path = Environment.GetEnvironmentVariable("PATH");
        if (path is null)
        {
            return null;
        }

        foreach (string directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            string candidate = Path.Combine(directory, name);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            string windows = candidate + ".exe";
            if (OperatingSystem.IsWindows() && File.Exists(windows))
            {
                return windows;
            }
        }

        return null;
    }
}
