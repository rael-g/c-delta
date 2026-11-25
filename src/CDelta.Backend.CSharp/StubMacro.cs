namespace CDelta.Backend.CSharp;

public enum StubMacroKind
{
    Integer,
    Float,
    Text,
}

public sealed record StubMacro(string Name, StubMacroKind Kind, string CType, Int128 Integer, double Float, string Text);
