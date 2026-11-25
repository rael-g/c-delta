namespace CDelta.Ir;

public readonly record struct IrParseResult(Module? Module, string? Error, int Line);
