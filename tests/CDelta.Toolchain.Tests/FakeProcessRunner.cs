using CDelta.Toolchain;

namespace CDelta.Toolchain.Tests;

internal sealed class FakeProcessRunner(Func<string, IReadOnlyList<string>, ToolResult> respond) : IProcessRunner
{
    public List<(string Executable, IReadOnlyList<string> Arguments)> Calls { get; } = [];

    public ToolResult Run(string executable, IReadOnlyList<string> arguments)
    {
        Calls.Add((executable, arguments));
        return respond(executable, arguments);
    }
}
