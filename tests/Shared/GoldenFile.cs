namespace CDelta.Tests;

internal static class GoldenFile
{
    private const string UpdateVariable = "CDELTA_UPDATE_GOLDEN";

    public static void AssertMatches(string path, string actual)
    {
        if (Environment.GetEnvironmentVariable(UpdateVariable) == "1")
        {
            System.IO.Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, actual);
            return;
        }

        Assert.Equal(File.ReadAllText(path), actual);
    }

    public static string AreaPath(string project, string area) =>
        Path.Combine(RepositoryRoot(), "tests", project, "Golden", area);

    public static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CDelta.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("repository root not found");
    }
}
