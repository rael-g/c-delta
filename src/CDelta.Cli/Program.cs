using System.Diagnostics;
using CDelta.Diagnostics;
using CDelta.Driver;
using CDelta.Toolchain;

namespace CDelta.Cli;

internal static class Program
{
    private const int InvalidInvocation = 2;
    private const string DefaultTarget = "native";
    private const string DefaultMode = "Debug";
    private const string DefaultToolchain = "auto";

    private static int Main(string[] args)
    {
        CommandLine? command = CommandLineParser.Parse(args);
        if (command is null)
        {
            Console.Error.WriteLine(CommandLine.Usage);
            return InvalidInvocation;
        }

        var runner = new ProcessRunner();
        ProjectPlan? plan = null;
        if (command.Files.Count == 1 && command.Files[0].EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
        {
            var loading = new DiagnosticBag();
            plan = ProjectLoader.Load(runner, command.Files[0], loading);
            if (plan is null)
            {
                foreach (CDelta.Diagnostics.Diagnostic item in loading.Items)
                {
                    Console.Error.WriteLine(DiagnosticFormatter.Format(item));
                }

                return 1;
            }
        }

        var options = new CompilerOptions(
            plan?.Sources ?? command.Files,
            command.Target == DefaultTarget && plan?.Target is { } projectTarget ? projectTarget : command.Target,
            command.Mode == DefaultMode && plan?.OptimizeMode is { } projectMode ? projectMode : command.Mode,
            command.Output,
            command.Toolchain == DefaultToolchain && plan?.Toolchain is { } projectToolchain ? projectToolchain : command.Toolchain,
            command.EmitC || plan?.EmitC == true,
            command.DumpIr,
            command.Verbose,
            command.Time,
            plan,
            command.Command == "cimport");
        var pipeline = new BuildPipeline(runner, Environment.GetEnvironmentVariable);
        BuildResult result = pipeline.Build(options);
        foreach (string message in result.Messages)
        {
            Console.Error.WriteLine(message);
        }

        if (result.ExitCode != 0 || command.Command is "build" or "cimport")
        {
            return result.ExitCode;
        }

        return RunProgram(result.OutputPath!, command.ProgramArguments);
    }

    private static int RunProgram(string path, IReadOnlyList<string> arguments)
    {
        var start = new ProcessStartInfo(path) { UseShellExecute = false };
        foreach (string argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(start)!;
        process.WaitForExit();
        return process.ExitCode;
    }
}
