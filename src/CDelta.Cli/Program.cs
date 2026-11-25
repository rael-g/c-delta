using System.Diagnostics;
using CDelta.Driver;
using CDelta.Toolchain;

namespace CDelta.Cli;

internal static class Program
{
    private const int InvalidInvocation = 2;

    private static int Main(string[] args)
    {
        CommandLine? command = CommandLineParser.Parse(args);
        if (command is null)
        {
            Console.Error.WriteLine(CommandLine.Usage);
            return InvalidInvocation;
        }

        var options = new CompilerOptions(
            command.Files,
            command.Target,
            command.Mode,
            command.Output,
            command.Toolchain,
            command.EmitC,
            command.DumpIr,
            command.Verbose,
            command.Time);
        var pipeline = new BuildPipeline(new ProcessRunner(), Environment.GetEnvironmentVariable);
        BuildResult result = pipeline.Build(options);
        foreach (string message in result.Messages)
        {
            Console.Error.WriteLine(message);
        }

        if (result.ExitCode != 0 || command.Command == "build")
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
