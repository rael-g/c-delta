using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace CDelta.Diagnostics.Generator;

[Generator]
public sealed class RegistryGenerator : IIncrementalGenerator
{
    private const string TableFile = "diagnostics.md";
    private const int CodeDigits = 4;

    private static readonly string[] _descriptorFields = ["CDG001", "Invalid diagnostics registry", "{0}", "CDelta.Diagnostics"];

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        IncrementalValuesProvider<AdditionalText> tables = context.AdditionalTextsProvider
            .Where(file => Path.GetFileName(file.Path) == TableFile);
        context.RegisterSourceOutput(tables, Generate);
    }

    private static void Generate(SourceProductionContext context, AdditionalText file)
    {
        var problems = new List<string>();
        IReadOnlyList<(string Code, string Message)> rows = RegistryTable.Read(file.GetText(context.CancellationToken)?.ToString() ?? string.Empty, problems);
        foreach (string problem in problems)
        {
            context.ReportDiagnostic(Diagnostic.Create(Descriptor(), Location.None, problem));
        }

        if (problems.Count > 0)
        {
            return;
        }

        var codes = new StringBuilder("namespace CDelta.Diagnostics;\n\npublic enum DiagnosticCode\n{\n");
        var registry = new StringBuilder(
            "namespace CDelta.Diagnostics;\n\npublic static class DiagnosticRegistry\n{\n"
            + "    public static IReadOnlyDictionary<DiagnosticCode, string> Messages { get; } = new Dictionary<DiagnosticCode, string>\n    {\n");
        foreach ((string code, string message) in rows)
        {
            codes.Append("    ").Append(code).Append(" = ").Append(int.Parse(code.Substring(2))).Append(",\n");
            registry.Append("        [DiagnosticCode.").Append(code).Append("] = ").Append(Literal(message)).Append(",\n");
        }

        codes.Append("}\n");
        registry.Append("    };\n}\n");
        context.AddSource("DiagnosticCode.g.cs", SourceText.From(codes.ToString(), Encoding.UTF8));
        context.AddSource("DiagnosticRegistry.g.cs", SourceText.From(registry.ToString(), Encoding.UTF8));
    }

    private static DiagnosticDescriptor Descriptor() =>
        new(_descriptorFields[0], _descriptorFields[1], _descriptorFields[2], _descriptorFields[3], DiagnosticSeverity.Error, true);

    private static string Literal(string value) =>
        "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
}
