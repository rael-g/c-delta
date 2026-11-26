namespace CDelta.Toolchain;

public sealed class ClangProvider(IProcessRunner runner, string executable) : IToolchainProvider
{
    private const int MinimumMajor = 18;

    public string Name => "clang";

    public string Executable { get; } = executable;

    public bool SupportsTarget(string target) => TargetTriples.Clang(target) is not null;

    public bool MeetsMinimumVersion(out string version)
    {
        ToolResult result = runner.Run(Executable, ["--version"]);
        version = Versions.FirstVersion(result.StandardOutput) ?? string.Empty;
        return result.ExitCode == 0 && Versions.TryParse(version, out int major, out _, out _) && major >= MinimumMajor;
    }

    public IReadOnlyList<string> SystemIncludeDirectories(string target) =>
        IncludeProbe.Directories(runner, Executable, ["--target=" + TargetTriples.Clang(target)]);

    public ToolResult Compile(CompileRequest request)
    {
        var arguments = new List<string> { "--target=" + TargetTriples.Clang(request.Target), "-c" };
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
        var arguments = new List<string> { "--target=" + TargetTriples.Clang(request.Target) };
        arguments.AddRange(request.Objects);
        arguments.Add("-o");
        arguments.Add(request.Output);
        CommonFlags.AddLibraries(arguments, request.Libraries, request.NativeLibraries ?? [], request.LibraryDirectories ?? []);
        return runner.Run(Executable, arguments);
    }
}
