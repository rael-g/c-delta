using CDelta.Frontend.CSharp;
using CDelta.Frontend.CSharp.Rules;
using CDelta.Tests;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace CDelta.Frontend.CSharp.Rules.Tests;

public sealed class RuleTests
{
    private const string Area = "Rules";

    public static IEnumerable<TheoryDataRow<string>> Cases() =>
        Directory.GetFiles(GoldenFile.AreaPath("CDelta.Frontend.CSharp.Rules.Tests", Area), "*.cs")
            .Order(StringComparer.Ordinal)
            .Select(path => new TheoryDataRow<string>(Path.GetFileNameWithoutExtension(path)));

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task ReportsExactlyTheExpectedDiagnostics(string name)
    {
        string directory = GoldenFile.AreaPath("CDelta.Frontend.CSharp.Rules.Tests", Area);
        string path = Path.Combine(directory, name + ".cs");
        CSharpCompilation compilation = CompilationFactory.Create(
            [new SourceInput(path, name + ".cs", File.ReadAllText(path))],
            Path.Combine(AppContext.BaseDirectory, "CDelta.Corelib.dll"),
            "Rules",
            []);

        System.Collections.Immutable.ImmutableArray<Diagnostic> all = await compilation
            .WithAnalyzers(SubsetAnalyzers.Create())
            .GetAllDiagnosticsAsync(TestContext.Current.CancellationToken);
        IEnumerable<string> lines = all
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .OrderBy(d => d.Location.SourceSpan.Start)
            .ThenBy(d => d.Id, StringComparer.Ordinal)
            .Select(d =>
            {
                LinePosition start = d.Location.GetLineSpan().StartLinePosition;
                return name + ".cs(" + (start.Line + 1) + "," + (start.Character + 1) + "): " + d.Id;
            });

        string text = string.Concat(lines.Select(l => l + "\n"));
        GoldenFile.AssertMatches(Path.Combine(directory, name + ".diagnostics"), text);
    }
}
