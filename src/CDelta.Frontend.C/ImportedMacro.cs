namespace CDelta.Frontend.C;

public enum MacroKind
{
    Integer,
    Float,
    Text,
}

public sealed record ImportedMacro(string Name, MacroKind Kind, string CType, Int128 Integer, double Float, string Text);
