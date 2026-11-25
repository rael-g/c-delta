using System.Runtime.InteropServices;

namespace CDelta.Driver;

public static class HostTarget
{
    private const string MuslLoaderPattern = "ld-musl-*";
    private const string MuslLoaderDirectory = "/lib";

    public static string? Resolve()
    {
        string? arch = RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X64 => "x86_64",
            Architecture.Arm64 => "aarch64",
            _ => null,
        };
        if (arch is null)
        {
            return null;
        }

        if (OperatingSystem.IsWindows())
        {
            return arch + "-windows-gnu";
        }

        if (OperatingSystem.IsMacOS())
        {
            return arch + "-macos-none";
        }

        if (OperatingSystem.IsLinux())
        {
            bool musl = Directory.Exists(MuslLoaderDirectory)
                && Directory.GetFiles(MuslLoaderDirectory, MuslLoaderPattern).Length > 0;
            return arch + "-linux-" + (musl ? "musl" : "gnu");
        }

        return null;
    }
}
