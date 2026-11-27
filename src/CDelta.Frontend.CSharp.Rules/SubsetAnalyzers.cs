using System.Collections.Immutable;
using Microsoft.CodeAnalysis.Diagnostics;

namespace CDelta.Frontend.CSharp.Rules;

public static class SubsetAnalyzers
{
    public static ImmutableArray<DiagnosticAnalyzer> Create() => [new SubsetAnalyzer(), new SubsetSuppressor()];
}
