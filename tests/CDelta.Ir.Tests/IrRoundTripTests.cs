using CDelta.Ir;
using CDelta.Tests;

namespace CDelta.Ir.Tests;

public sealed class IrRoundTripTests
{
    public static IEnumerable<TheoryDataRow<string>> GoldenFiles()
    {
        string tests = Path.Combine(GoldenFile.RepositoryRoot(), "tests");
        return Directory.GetFiles(tests, "*.ir", SearchOption.AllDirectories)
            .Where(path => path.Contains("Golden", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .Select(path => new TheoryDataRow<string>(Path.GetRelativePath(tests, path)));
    }

    [Theory]
    [MemberData(nameof(GoldenFiles))]
    public void PrintOfParseIsTheText(string relativePath)
    {
        string text = File.ReadAllText(Path.Combine(GoldenFile.RepositoryRoot(), "tests", relativePath));

        IrParseResult parsed = IrParser.Parse(text);

        Assert.True(parsed.Module is not null, parsed.Error + " (line " + parsed.Line + ")");
        Assert.Equal(text, IrPrinter.Print(parsed.Module!));
    }

    [Fact]
    public void ParseOfPrintIsTheSameModule()
    {
        Module module = SampleModules.Arithmetic(ModuleStage.Lowered, ArithMode.Default);

        Module parsed = IrParser.Parse(IrPrinter.Print(module)).Module!;

        Assert.Equal(IrPrinter.Print(module), IrPrinter.Print(parsed));
        Assert.Equal(module.Functions.Count, parsed.Functions.Count);
        Assert.Equal(module.Bodies[0]!.Statements.Count, parsed.Bodies[0]!.Statements.Count);
    }

    [Fact]
    public void AMalformedTextIsAnErrorValue()
    {
        IrParseResult parsed = IrParser.Parse("module X stage lowered\n\nfn broken(\n");

        Assert.Null(parsed.Module);
        Assert.NotNull(parsed.Error);
    }
}
