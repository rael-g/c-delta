using System.Diagnostics;
using CDelta.Driver;

namespace CDelta.Conformance.Tests;

internal static class ProgramRunner
{
    private const string AArch64Target = "aarch64-linux-gnu";
    private const string WindowsTarget = "x86_64-windows-gnu";
    private const string SysrootVariable = "CDELTA_AARCH64_SYSROOT";
    private const string DefaultSysroot = "/usr/aarch64-linux-gnu";
    private const string WineBanner = "it looks like wine32 is missing";
    private const int WineBannerLines = 4;

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

        if (target == WindowsTarget)
        {
            start.Environment["WINEDEBUG"] = "-all";
        }

        using Process process = Process.Start(start)!;
        Task<string> error = process.StandardError.ReadToEndAsync();
        string output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return target == WindowsTarget
            ? (process.ExitCode, output.Replace("\r\n", "\n"), WithoutWineBanner(error.Result.Replace("\r\n", "\n")))
            : (process.ExitCode, output, error.Result);
    }

    private static string WithoutWineBanner(string error) =>
        error.StartsWith(WineBanner, StringComparison.Ordinal)
            ? string.Join('\n', error.Split('\n').Skip(WineBannerLines))
            : error;

    private static (string executable, string[] arguments)? Command(string target, string program)
    {
        string? host = HostTarget.Resolve();
        if (target == host)
        {
            return (program, []);
        }

        if (target == AArch64Target && host == "x86_64-linux-gnu")
        {
            return ("qemu-aarch64", ["-L", Environment.GetEnvironmentVariable(SysrootVariable) ?? DefaultSysroot, program]);
        }

        if (target == WindowsTarget && OperatingSystem.IsLinux())
        {
            return ("wine", [program]);
        }

        return null;
    }
}
