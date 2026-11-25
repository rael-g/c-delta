namespace CDelta.Toolchain;

public readonly record struct ToolResult(int ExitCode, string StandardOutput, string StandardError, string CommandLine);
