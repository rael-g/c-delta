using CDelta.Diagnostics;
using Microsoft.CodeAnalysis;

namespace CDelta.Frontend.CSharp.Rules;

public static class Descriptors
{
    private const string Category = "CDelta";

    private static readonly Dictionary<DiagnosticCode, DiagnosticDescriptor> _byCode = Build();

    public static IReadOnlyCollection<DiagnosticDescriptor> All => _byCode.Values;

    public static DiagnosticDescriptor For(DiagnosticCode code) => _byCode[code];

    private static Dictionary<DiagnosticCode, DiagnosticDescriptor> Build()
    {
        var result = new Dictionary<DiagnosticCode, DiagnosticDescriptor>();
        foreach (KeyValuePair<DiagnosticCode, string> entry in DiagnosticRegistry.Messages)
        {
            string message = entry.Value.Replace("{", "{{").Replace("}", "}}");
            result.Add(
                entry.Key,
                new DiagnosticDescriptor(entry.Key.ToString(), message, message, Category, DiagnosticSeverity.Error, true));
        }

        return result;
    }
}
