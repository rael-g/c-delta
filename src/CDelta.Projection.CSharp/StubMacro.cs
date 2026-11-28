using CDelta.Ir;

namespace CDelta.Projection.CSharp;

public enum StubMacroKind
{
    Integer,
    Float,
    Text,
    Address,
}

public sealed record StubMacro(string Name, StubMacroKind Kind, string CType, Int128 Integer, double Float, string Text, TypeId Type = default);
