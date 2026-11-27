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
        Assert.False(bag.HasError, string.Join("; ", bag.Items.Select(d => d.Code + " " + string.Join(" ", d.Notes.Select(n => n.Text)))));
        return final!;
    }

    public static string Generate(Module final, bool lineDirectives) =>
        CBackend.Generate(final, new BackendOptions(lineDirectives, true)).Program.Replace(GoldenFile.RepositoryRoot(), "$ROOT");

    public static (int ExitCode, string Error) Run(Module final, OptimizeMode mode)
    {
        string directory = Directory.CreateTempSubdirectory("cdelta-backend").FullName;
        try
        {
            CBackendOutput generated = CBackend.Generate(final, new BackendOptions(false, true));
            var sources = new List<string>();
            File.WriteAllText(Path.Combine(directory, "program.c"), generated.Program);
            sources.Add(Path.Combine(directory, "program.c"));
            if (generated.Libc is not null)
            {
                File.WriteAllText(Path.Combine(directory, "libc.c"), generated.Libc);
                sources.Add(Path.Combine(directory, "libc.c"));
            }

            var runner = new ProcessRunner();
            var bag = new DiagnosticBag();
            IToolchainProvider? toolchain = ToolchainSelector.Select("auto", HostTarget, runner, Environment.GetEnvironmentVariable, PathSearch.Find, bag);
            Assert.NotNull(toolchain);
            var objects = new List<string>();
            foreach (string source in sources)
            {
                string objectFile = Path.ChangeExtension(source, ".o");
                ToolResult compiled = toolchain.Compile(new CompileRequest(source, objectFile, HostTarget, mode, [], []));
                Assert.True(compiled.ExitCode == 0, compiled.CommandLine + "\n" + compiled.StandardError);
                objects.Add(objectFile);
            }

            string program = Path.Combine(directory, "program");
            ToolResult linked = toolchain.Link(new LinkRequest(objects, program, HostTarget, mode, []));
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
