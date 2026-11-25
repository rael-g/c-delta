using System.Diagnostics;
using CDelta.Driver;

namespace CDelta.Conformance.Tests;

internal static class ProgramRunner
{
    private const string AArch64Target = "aarch64-linux-gnu";
    private const string WindowsTarget = "x86_64-windows-gnu";
    private const string SysrootVariable = "CDELTA_AARCH64_SYSROOT";

    public static bool CanRun(string target) => Command(target, "program") is not null;

    public static (int ExitCode, string StandardOutput, string StandardError) Run(string target, string program)
    {
        (string executable, string[] arguments)? command = Command(target, program);
        Assert.True(command is not null, "no way to run " + target);
        var start = new ProcessStartInfo(command!.Value.executable)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (string argument in command.Value.arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(start)!;
        Task<string> error = process.StandardError.ReadToEndAsync();
        string output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return (process.ExitCode, output, error.Result);
    }

    private static (string executable, string[] arguments)? Command(string target, string program)
    {
        string? host = HostTarget.Resolve();
        if (target == host)
        {
            return (program, []);
        }

        if (target == AArch64Target && host == "x86_64-linux-gnu")
        {
            return ("qemu-aarch64", ["-L", Environment.GetEnvironmentVariable(SysrootVariable) ?? string.Empty, program]);
        }

        if (target == WindowsTarget && OperatingSystem.IsLinux())
        {
            return ("wine", [program]);
        }

        return null;
    }
}
