using CDelta.Ir;

namespace CDelta.Frontend.C;

public enum MacroKind
{
    Integer,
    Float,
    Text,
    Address,
}

public sealed record ImportedMacro(string Name, MacroKind Kind, string CType, Int128 Integer, double Float, string Text, TypeId Type = default);
