using System.Diagnostics;
using System.Text;

namespace CDelta.Toolchain;

public sealed class ProcessRunner : IProcessRunner
{
    public ToolResult Run(string executable, IReadOnlyList<string> arguments)
    {
        var info = new ProcessStartInfo(executable)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (string argument in arguments)
        {
            info.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = info };
        var output = new StringBuilder();
        var error = new StringBuilder();
        process.OutputDataReceived += (_, e) => output.AppendLine(e.Data);
        process.ErrorDataReceived += (_, e) => error.AppendLine(e.Data);
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        process.WaitForExit();
        return new ToolResult(process.ExitCode, output.ToString(), error.ToString(), CommandLine(executable, arguments));
    }

    public static string CommandLine(string executable, IReadOnlyList<string> arguments)
    {
        var text = new StringBuilder(executable);
        foreach (string argument in arguments)
        {
            text.Append(' ').Append(argument.Contains(' ') ? "\"" + argument + "\"" : argument);
        }

        return text.ToString();
    }
}
