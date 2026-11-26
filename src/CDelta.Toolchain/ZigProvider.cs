namespace CDelta.Toolchain;

public sealed class ZigProvider(IProcessRunner runner, string executable) : IToolchainProvider
{
    private const int MinimumMajor = 0;
    private const int MinimumMinor = 15;
    private const int MinimumPatch = 1;

    public string Name => "zig";

    public string Executable { get; } = executable;

    public bool SupportsTarget(string target) => TargetTriples.Zig(target) is not null;

    public bool MeetsMinimumVersion(out string version)
    {
        ToolResult result = runner.Run(Executable, ["version"]);
        version = result.StandardOutput.Trim();
        if (result.ExitCode != 0 || !Versions.TryParse(version, out int major, out int minor, out int patch))
        {
            return false;
        }

        return (major, minor, patch).CompareTo((MinimumMajor, MinimumMinor, MinimumPatch)) >= 0;
    }

    public IReadOnlyList<string> SystemIncludeDirectories(string target) =>
        IncludeProbe.Directories(runner, Executable, ["cc", "-target", TargetTriples.Zig(target)!]);

    public ToolResult Compile(CompileRequest request)
    {
        var arguments = new List<string> { "cc", "-target", TargetTriples.Zig(request.Target)!, "-c" };
        CommonFlags.AddCompileFlags(arguments, request.Mode);
        foreach (string directory in request.IncludeDirectories)
        {
            arguments.Add("-I" + directory);
        }

        foreach (string define in request.Defines)
        {
            arguments.Add("-D" + define);
        }

        arguments.AddRange(request.ExtraFlags ?? []);
        arguments.Add(request.Source);
        arguments.Add("-o");
        arguments.Add(request.Output);
        return runner.Run(Executable, arguments);
    }

    public ToolResult Link(LinkRequest request)
    {
        var arguments = new List<string> { "cc", "-target", TargetTriples.Zig(request.Target)! };
        arguments.AddRange(request.Objects);
        arguments.Add("-o");
        arguments.Add(request.Output);
        CommonFlags.AddLibraries(arguments, request.Libraries, request.NativeLibraries ?? [], request.LibraryDirectories ?? []);
        return runner.Run(Executable, arguments);
    }
}
