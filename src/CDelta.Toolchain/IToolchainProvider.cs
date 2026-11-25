namespace CDelta.Toolchain;

public interface IToolchainProvider
{
    string Name { get; }

    string Executable { get; }

    bool SupportsTarget(string target);

    bool MeetsMinimumVersion(out string version);

    ToolResult Compile(CompileRequest request);

    ToolResult Link(LinkRequest request);
}
