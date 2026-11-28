namespace CDelta.Toolchain;

public interface IToolchainProvider
{
    string Name { get; }

    string Executable { get; }

    bool SupportsTarget(string target);

    bool MeetsMinimumVersion(out string version);

    IReadOnlyList<string> SystemIncludeDirectories(string target);

    ToolResult Compile(CompileRequest request);

    ToolResult Link(LinkRequest request);

    ToolResult Archive(ArchiveRequest request);
}
