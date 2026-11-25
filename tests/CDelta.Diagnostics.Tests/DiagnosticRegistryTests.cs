using System.Text.RegularExpressions;
using CDelta.Diagnostics;

namespace CDelta.Diagnostics.Tests;

public sealed partial class DiagnosticRegistryTests
{
    private const string RegistryHeading = "## Registry";
    private const string RegistryEnd = "## In the code";

    [GeneratedRegex(@"^\| (CD\d{4}) \| (.*) \| \[[^\]]*\]\([^)]*\) \|$")]
    private static partial Regex RowPattern();

    [Fact]
    public void RegistryMatchesTheDocument()
    {
        Dictionary<string, string> documented = ReadDocumentedRegistry();
        var actual = DiagnosticRegistry.Messages.ToDictionary(p => p.Key.ToString(), p => p.Value);

        Assert.Equal(documented.Keys.Order(), actual.Keys.Order());
        foreach ((string code, string message) in documented)
        {
            Assert.Equal(message, actual[code]);
        }
    }

    [Fact]
    public void EveryCodeHasItsNumberAsValue()
    {
        foreach (DiagnosticCode code in Enum.GetValues<DiagnosticCode>())
        {
            Assert.Equal("CD" + ((int)code).ToString("D4"), code.ToString());
            Assert.True(DiagnosticRegistry.Messages.ContainsKey(code));
        }
    }

    [Fact]
    public void BagReportsErrors()
    {
        var bag = new DiagnosticBag();
        Assert.False(bag.HasError);
        bag.Add(DiagnosticCode.CD3004, new Location("a.cs", 1, 1));
        Assert.True(bag.HasError);
    }

    private static Dictionary<string, string> ReadDocumentedRegistry()
    {
        string path = Path.Combine(RepositoryRoot(), "docs", "compiler", "diagnostics.md");
        var rows = new Dictionary<string, string>();
        bool inRegistry = false;
        foreach (string line in File.ReadAllLines(path))
        {
            if (line.StartsWith(RegistryHeading, StringComparison.Ordinal))
            {
                inRegistry = true;
                continue;
            }

            if (line.StartsWith(RegistryEnd, StringComparison.Ordinal))
            {
                break;
            }

            Match match = RowPattern().Match(line);
            if (inRegistry && match.Success)
            {
                rows.Add(match.Groups[1].Value, match.Groups[2].Value.Replace('`', '\''));
            }
        }

        return rows;
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CDelta.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("repository root not found");
    }
}
