using System.Diagnostics;
using CDelta.Driver;
using CDelta.Toolchain;

namespace CDelta.Conformance.Tests;

public sealed class StrictCTests
{
    private const string Mode = "Debug";

    public static IEnumerable<TheoryDataRow<string>> Cases() =>
        CaseDiscovery.Cases()
            .Where(c => c.Mode == Mode && c.Target == (HostTarget.Resolve() ?? "x86_64-linux-gnu"))
            .Where(c => !File.Exists(Path.Combine(CaseDiscovery.Root(), c.RelativePath[..^".cs".Length] + ".diagnostics")))
            .Select(c => new TheoryDataRow<string>(c.RelativePath));

    [Theory]
    [MemberData(nameof(Cases))]
    public void GeneratedCIsStrictAndSanitizerClean(string relativePath)
    {
        string source = Path.Combine(CaseDiscovery.Root(), relativePath);
        string stem = source[..^".cs".Length];
        string directory = Directory.CreateTempSubdirectory("cdelta-strict").FullName;
        try
        {
            string target = HostTarget.Resolve() ?? "x86_64-linux-gnu";
            string isolated = Path.Combine(directory, "case.cs");
            File.Copy(source, isolated);
            var options = new CompilerOptions([isolated], target, Mode, Path.Combine(directory, "reference"), "auto", true, null, false, false);
            BuildResult result = new BuildPipeline(new ProcessRunner(), Environment.GetEnvironmentVariable).Build(options);
            Assert.True(result.ExitCode == 0, string.Join("\n", result.Messages));

            string program = Path.Combine(directory, "sanitized");
            foreach (string compiler in new[] { "clang", "gcc" })
            {
                var arguments = new List<string> { "-std=c11", "-Wall", "-Wextra", "-pedantic", "-O0", "-g" };
                if (compiler == "clang")
                {
                    arguments.Add("-Werror");
                }

                if (compiler == "gcc")
                {
                    arguments.Add("-fsanitize=address,undefined");
                    arguments.Add("-fno-sanitize-recover=undefined");
                }
                else
                {
                    arguments.Add("-fsyntax-only");
                }

                arguments.AddRange(result.GeneratedC);
                if (compiler == "gcc")
                {
                    arguments.Add("-o");
                    arguments.Add(program);
                }

                ToolResult compiled = new ProcessRunner().Run(compiler, arguments);
                Assert.True(compiled.ExitCode == 0, compiled.CommandLine + "\n" + compiled.StandardError);
            }

            if (File.Exists(stem + ".panic"))
            {
                return;
            }

            var start = new ProcessStartInfo(program) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
            using Process process = Process.Start(start)!;
            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();
            process.WaitForExit();
            int expectedExit = File.Exists(stem + ".exitcode") ? int.Parse(File.ReadAllText(stem + ".exitcode").Trim()) : 0;
            Assert.True(process.ExitCode == expectedExit, "exit " + process.ExitCode + "\n" + error);
            if (File.Exists(stem + ".stdout"))
            {
                Assert.Equal(File.ReadAllText(stem + ".stdout"), output);
            }
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }
}
