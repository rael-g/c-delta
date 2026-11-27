using System.Text.RegularExpressions;

namespace CDelta.Diagnostics.Generator;

internal static class RegistryTable
{
    private const string Heading = "## Registry";
    private const string End = "## In the code";
    private const string RowPattern = @"^\| (CD\d{4}) \| (.*) \| \[[^\]]*\]\([^)]*\) \|$";

    public static IReadOnlyList<(string Code, string Message)> Read(string text, List<string> problems)
    {
        var rows = new List<(string Code, string Message)>();
        var seen = new HashSet<string>();
        bool inRegistry = false;
        foreach (string line in text.Split('\n'))
        {
            string trimmed = line.TrimEnd('\r');
            if (trimmed.StartsWith(Heading, StringComparison.Ordinal))
            {
                inRegistry = true;
                continue;
            }

            if (trimmed.StartsWith(End, StringComparison.Ordinal))
            {
                break;
            }

            if (!inRegistry || !trimmed.StartsWith("| CD", StringComparison.Ordinal))
            {
                continue;
            }

            Match match = Regex.Match(trimmed, RowPattern);
            if (!match.Success)
            {
                problems.Add("malformed registry row: " + trimmed);
                continue;
            }

            string code = match.Groups[1].Value;
            if (!seen.Add(code))
            {
                problems.Add("duplicated registry code " + code);
                continue;
            }

            rows.Add((code, match.Groups[2].Value.Replace('`', '\'')));
        }

        return rows;
    }
}
