namespace CDelta.Ir;

internal sealed class IrParseException(string message, int line) : Exception(message)
{
    public int Line { get; } = line;
}
