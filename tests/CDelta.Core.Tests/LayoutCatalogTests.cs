using System.Diagnostics;
using System.Text.RegularExpressions;
using CDelta.Ir;

namespace CDelta.Core.Tests;

public sealed partial class LayoutCatalogTests
{
    private const int BitsPerByte = 8;

    private static readonly (string Target, string Clang)[] _targets =
    [
        ("x86_64-linux-gnu", "x86_64-unknown-linux-gnu"),
        ("x86_64-linux-musl", "x86_64-unknown-linux-musl"),
        ("aarch64-linux-gnu", "aarch64-unknown-linux-gnu"),
        ("x86_64-windows-gnu", "x86_64-w64-windows-gnu"),
        ("aarch64-macos-none", "arm64-apple-macos"),
        ("x86_64-windows-msvc", "x86_64-pc-windows-msvc"),
        ("wasm32-wasi-musl", "wasm32-unknown-wasi"),
    ];

    public static IEnumerable<TheoryDataRow<string, string>> Targets() =>
        _targets.Select(t => new TheoryDataRow<string, string>(t.Target, t.Clang));

    [GeneratedRegex(@"Type: struct (\w+)\s+Layout: <ASTRecordLayout\s+Size:(\d+)\s+(?:DataSize:\d+\s+)?Alignment:(\d+)\s+(?:\w+:\d+\s+)*FieldOffsets: \[([^\]]*)\]")]
    private static partial Regex RecordLayout();

    [Theory]
    [MemberData(nameof(Targets))]
    public void MatchesClang(string targetName, string clangTriple)
    {
        Assert.True(TargetTable.TryGet(targetName, out Target target));
        Module module = CatalogModule.Build(LayoutCatalog.Cases);
        LayoutPass.Run(module, target);

        string directory = Directory.CreateTempSubdirectory("cdelta-layout").FullName;
        try
        {
            string source = Path.Combine(directory, "catalog.c");
            File.WriteAllText(source, CatalogModule.CSource(LayoutCatalog.Cases, target.NativeInt128));
            string dump = RunClang(clangTriple, source);

            var expected = new Dictionary<string, (int Size, int Alignment, int[] Offsets)>();
            foreach (Match match in RecordLayout().Matches(dump))
            {
                int[] offsets = match.Groups[4].Value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(o => int.Parse(o) / BitsPerByte)
                    .ToArray();
                expected[match.Groups[1].Value] = (int.Parse(match.Groups[2].Value) / BitsPerByte, int.Parse(match.Groups[3].Value) / BitsPerByte, offsets);
            }

            foreach (CatalogStruct entry in LayoutCatalog.Cases)
            {
                StructDecl actual = module.Structs[module.StructsByKey[entry.Name].Index];
                (int size, int alignment, int[] offsets) = expected[entry.Name];
                int[] actualOffsets = Enumerable.Range(0, actual.Fields.Count)
                    .Select(i => module.Fields[actual.Fields.Start + i].Offset)
                    .ToArray();
                Assert.True(
                    (size, alignment) == (actual.Size, actual.Alignment) && offsets.SequenceEqual(actualOffsets),
                    entry.Name + " on " + targetName + ": clang size " + size + " align " + alignment + " offsets ["
                    + string.Join(",", offsets) + "], computed size " + actual.Size + " align " + actual.Alignment
                    + " offsets [" + string.Join(",", actualOffsets) + "]");
            }
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    private static string RunClang(string triple, string source)
    {
        var start = new ProcessStartInfo("clang")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (string argument in new[] { "--target=" + triple, "-nostdinc", "-S", "-emit-llvm", "-o", "/dev/null", "-Xclang", "-fdump-record-layouts-simple", source })
        {
            start.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(start)!;
        Task<string> error = process.StandardError.ReadToEndAsync();
        string output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        Assert.True(process.ExitCode == 0, error.Result);
        return output;
    }
}
