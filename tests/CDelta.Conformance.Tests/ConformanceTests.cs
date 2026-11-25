using System.Text.RegularExpressions;
using CDelta.Driver;
using CDelta.Toolchain;

namespace CDelta.Conformance.Tests;

public sealed partial class ConformanceTests
{
    private const string PanicPrefix = "panic: ";

    [GeneratedRegex(@"^(.*)\((\d+),(\d+)\): error (\S+):")]
    private static partial Regex DiagnosticLine();

    public static IEnumerable<TheoryDataRow<string, string, string>> Cases() =>
        CaseDiscovery.Cases().Select(c => new TheoryDataRow<string, string, string>(c.RelativePath, c.Mode, c.Target));

    [Theory]
    [MemberData(nameof(Cases))]
    public void Case(string relativePath, string mode, string target)
    {
        string source = Path.Combine(CaseDiscovery.Root(), relativePath);
        string stem = source[..^".cs".Length];
        string directory = Directory.CreateTempSubdirectory("cdelta-conformance").FullName;
        try
        {
            string output = Path.Combine(directory, "program" + (target.Contains("-windows-", StringComparison.Ordinal) ? ".exe" : string.Empty));
            var options = new CompilerOptions([source], target, mode, output, "auto", false, null, false, false);
            BuildResult result = new BuildPipeline(new ProcessRunner(), Environment.GetEnvironmentVariable).Build(options);

            if (File.Exists(stem + ".diagnostics"))
            {
                AssertDiagnostics(stem + ".diagnostics", result);
                return;
            }

            Assert.True(result.ExitCode == 0, string.Join("\n", result.Messages));
            if (!ProgramRunner.CanRun(target))
            {
                return;
            }

            (int exitCode, string standardOutput, string standardError) = ProgramRunner.Run(target, output);
            AssertRun(stem, exitCode, standardOutput, standardError);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    private static void AssertDiagnostics(string expectedPath, BuildResult result)
    {
        Assert.NotEqual(0, result.ExitCode);
        var actual = new List<string>();
        foreach (string message in result.Messages)
        {
            Match match = DiagnosticLine().Match(message);
            if (match.Success)
            {
                actual.Add(Path.GetFileName(match.Groups[1].Value) + "(" + match.Groups[2].Value + "," + match.Groups[3].Value + "): " + match.Groups[4].Value);
            }
        }

        string[] expected = File.ReadAllLines(expectedPath).Where(l => l.Length > 0).ToArray();
        Assert.Equal(expected, actual);
    }

    private static void AssertRun(string stem, int exitCode, string standardOutput, string standardError)
    {
        if (File.Exists(stem + ".panic"))
        {
            string message = File.ReadAllText(stem + ".panic").TrimEnd('\n');
            Assert.NotEqual(0, exitCode);
            Assert.Equal(PanicPrefix + message, standardError.Split('\n')[0]);
            return;
        }

        int expectedExit = File.Exists(stem + ".exitcode") ? int.Parse(File.ReadAllText(stem + ".exitcode").Trim()) : 0;
        Assert.Equal(expectedExit, exitCode);
        if (File.Exists(stem + ".stdout"))
        {
            Assert.Equal(File.ReadAllText(stem + ".stdout"), standardOutput);
        }
    }
}
