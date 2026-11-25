namespace CDelta.Toolchain;

public interface IProcessRunner
{
    ToolResult Run(string executable, IReadOnlyList<string> arguments);
}
