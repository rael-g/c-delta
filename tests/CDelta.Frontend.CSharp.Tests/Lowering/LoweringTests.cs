using CDelta.Diagnostics;
using CDelta.Frontend.CSharp;
using CDelta.Ir;
using CDelta.Tests;

namespace CDelta.Frontend.CSharp.Tests.Lowering;

public sealed class LoweringTests
{
    private const string CorelibFileName = "CDelta.Corelib.dll";

    public static IEnumerable<TheoryDataRow<string>> Cases() =>
        Directory.GetFiles(GoldenFile.AreaPath("CDelta.Frontend.CSharp.Tests", "Lowering"), "*.cs")
            .Select(path => new TheoryDataRow<string>(Path.GetFileNameWithoutExtension(path)));

    [Theory]
    [MemberData(nameof(Cases))]
    public void LowersToTheExpectedIr(string name)
    {
        string directory = GoldenFile.AreaPath("CDelta.Frontend.CSharp.Tests", "Lowering");
        string sourcePath = Path.Combine(directory, name + ".cs");
        var bag = new DiagnosticBag();
        var frontend = CSharpFrontend.Create(
            [new SourceInput(sourcePath, name + ".cs", File.ReadAllText(sourcePath))],
            Path.Combine(AppContext.BaseDirectory, CorelibFileName),
            "Golden",
            bag);

        Assert.Empty(frontend.BindingErrors);
        frontend.DeclareRoots();
        for (int i = 0; i < frontend.Module.Functions.Count; i++)
        {
            Body? body = frontend.Lower(new FunctionId(i));
            if (body is not null)
            {
                frontend.Builder.SetBody(new FunctionId(i), body);
            }
        }

        Assert.False(bag.HasError, string.Join("\n", bag.Items.Select(d => d.Code + " " + string.Join(";", d.Notes.Select(n => n.Text)))));
        DiagnosticBag verified = IrVerifier.Verify(frontend.Module, ModuleStage.Lowered);
        Assert.False(verified.HasError, string.Join("\n", verified.Items.Select(d => string.Join(";", d.Notes.Select(n => n.Text)))));
        string text = IrPrinter.Print(frontend.Module).Replace(GoldenFile.RepositoryRoot(), "$ROOT");
        GoldenFile.AssertMatches(Path.Combine(directory, name + ".ir"), text);
    }
}
