using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace CDelta.Frontend.CSharp.Tests.Probes;

internal static class CorelibCompilation
{
    private const string CorelibFileName = "CDelta.Corelib.dll";
    private const string MissingExternBodyId = "CS0626";

    private const string GlobalUsings = """
        global using System;
        global using System.Runtime.CompilerServices;
        global using System.Runtime.InteropServices;
        global using CDelta;
        global using static CDelta.Intrinsics;
        """;

    public static IReadOnlyList<string> Errors(string source)
    {
        var parse = new CSharpParseOptions(LanguageVersion.CSharp14);
        CSharpCompilationOptions options = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
            .WithAllowUnsafe(true)
            .WithOverflowChecks(false)
            .WithNullableContextOptions(NullableContextOptions.Disable)
            .WithDeterministic(true)
            .WithSpecificDiagnosticOptions(new Dictionary<string, ReportDiagnostic>
            {
                [MissingExternBodyId] = ReportDiagnostic.Suppress,
            });
        MetadataReference corelib = MetadataReference.CreateFromFile(Path.Combine(AppContext.BaseDirectory, CorelibFileName));
        var compilation = CSharpCompilation.Create(
            "Probe",
            [CSharpSyntaxTree.ParseText(GlobalUsings, parse), CSharpSyntaxTree.ParseText(source, parse)],
            [corelib],
            options);

        return compilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .Select(d => d.ToString())
            .ToList();
    }

    public static void AssertBinds(string source)
    {
        IReadOnlyList<string> errors = Errors(source);
        Assert.True(errors.Count == 0, string.Join(Environment.NewLine, errors));
    }
}
