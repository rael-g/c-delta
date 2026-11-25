using System.Diagnostics;
using CDelta.Backend.C;
using CDelta.Core;
using CDelta.Core.Tests;
using CDelta.Diagnostics;
using CDelta.Ir;
using CDelta.Tests;
using CDelta.Toolchain;

namespace CDelta.Backend.C.Tests;

internal static class CompiledProgram
{
    private const string HostTarget = "x86_64-linux-gnu";

    public static Module Final(Module lowered, BuildMode mode)
    {
        TargetTable.TryGet(HostTarget, out Target target);
        var bag = new DiagnosticBag();
        Module? final = CorePipeline.Run(lowered, new DictionaryBodyProvider(lowered), new CoreOptions(mode, target), bag);
        Assert.False(bag.HasError);
        return final!;
    }

    public static string Generate(Module final, bool lineDirectives) =>
        CBackend.Generate(final, new BackendOptions(lineDirectives, true)).Replace(GoldenFile.RepositoryRoot(), "$ROOT");

    public static (int ExitCode, string Error) Run(Module final, OptimizeMode mode)
    {
        string directory = Directory.CreateTempSubdirectory("cdelta-backend").FullName;
        try
        {
            string source = Path.Combine(directory, "program.c");
            File.WriteAllText(source, CBackend.Generate(final, new BackendOptions(false, true)));
            var runner = new ProcessRunner();
            var bag = new DiagnosticBag();
            IToolchainProvider? toolchain = ToolchainSelector.Select("auto", HostTarget, runner, Environment.GetEnvironmentVariable, PathSearch.Find, bag);
            Assert.NotNull(toolchain);
            string objectFile = Path.Combine(directory, "program.o");
            string program = Path.Combine(directory, "program");
            ToolResult compiled = toolchain.Compile(new CompileRequest(source, objectFile, HostTarget, mode, [], []));
            Assert.True(compiled.ExitCode == 0, compiled.CommandLine + "\n" + compiled.StandardError);
            ToolResult linked = toolchain.Link(new LinkRequest([objectFile], program, HostTarget, mode, []));
            Assert.True(linked.ExitCode == 0, linked.CommandLine + "\n" + linked.StandardError);

            var start = new ProcessStartInfo(program) { RedirectStandardError = true, UseShellExecute = false };
            using Process process = Process.Start(start)!;
            string error = process.StandardError.ReadToEnd();
            process.WaitForExit();
            return (process.ExitCode, error);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }
}
