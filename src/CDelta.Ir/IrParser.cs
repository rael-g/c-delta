namespace CDelta.Ir;

public static class IrParser
{
    public static IrParseResult Parse(string text) => Parse(text, null);

    public static IrParseResult Parse(string text, Func<string, Target?>? targets)
    {
        try
        {
            return new IrParseResult(new ModuleParser(text, targets).Run(), null, 0);
        }
        catch (IrParseException exception)
        {
            return new IrParseResult(null, exception.Message, exception.Line);
        }
    }
}
