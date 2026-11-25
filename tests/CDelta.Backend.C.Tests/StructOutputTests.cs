using CDelta.Core;
using CDelta.Core.Tests;
using CDelta.Ir;
using CDelta.Tests;
using CDelta.Toolchain;

namespace CDelta.Backend.C.Tests;

public sealed class StructOutputTests
{
    private static readonly string[] _targets =
    [
        "x86_64-linux-gnu",
        "x86_64-linux-musl",
        "aarch64-linux-gnu",
        "x86_64-windows-gnu",
        "aarch64-macos-none",
        "x86_64-windows-msvc",
        "wasm32-wasi-musl",
    ];

    public static IEnumerable<TheoryDataRow<string>> Targets() => _targets.Select(t => new TheoryDataRow<string>(t));

    [Fact]
    public void StructsAreDefinedWithAssertions()
    {
        Module module = Catalog("x86_64-linux-gnu");

        string text = CBackend.Generate(module, new BackendOptions(false, true)).Program;

        GoldenFile.AssertMatches(
            Path.Combine(GoldenFile.AreaPath("CDelta.Backend.C.Tests", "Output"), "catalog.c"),
            text);
    }

    [Theory]
    [MemberData(nameof(Targets))]
    public void TheCCompilerAcceptsTheLayoutAssertions(string target)
    {
        Module module = Catalog(target);
        string directory = Directory.CreateTempSubdirectory("cdelta-structs").FullName;
        try
        {
            string source = Path.Combine(directory, "catalog.c");
            File.WriteAllText(source, CBackend.Generate(module, new BackendOptions(false, true)).Program);
            var runner = new ProcessRunner();
            var bag = new CDelta.Diagnostics.DiagnosticBag();
            IToolchainProvider? toolchain = ToolchainSelector.Select("auto", target, runner, Environment.GetEnvironmentVariable, PathSearch.Find, bag);
            Assert.NotNull(toolchain);

            ToolResult result = toolchain.Compile(new CompileRequest(source, Path.Combine(directory, "catalog.o"), target, OptimizeMode.Debug, [], []));

            Assert.True(result.ExitCode == 0, result.CommandLine + "\n" + result.StandardError);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Theory]
    [InlineData("x86_64-linux-gnu")]
    [InlineData("aarch64-linux-gnu")]
    [InlineData("x86_64-windows-gnu")]
    public void ExplicitMinimumSizeAndAutoStructsCompile(string target)
    {
        var module = new Module("Special");
        var builder = new ModuleBuilder(module);
        TypeId i8 = builder.Primitive(TypeKind.I8);
        TypeId i32 = builder.Primitive(TypeKind.I32);
        TypeId f64 = builder.Primitive(TypeKind.F64);
        FieldDecl Field(string name, TypeId type, int offset) => new(module.Intern(name), type, IdRange.Empty, 0, offset, FieldDecl.NoBitOffset);
        builder.DeclareStruct("Union", "Union", SourceSpan.None, Origin.Synthetic, [Field("Int", i32, 0), Field("Real", f64, 0), Field("Byte", i8, 0)], StructLayoutKind.Explicit, 0, 0, []);
        builder.DeclareStruct("Holes", "Holes", SourceSpan.None, Origin.Synthetic, [Field("A", i32, 0), Field("B", i32, 8)], StructLayoutKind.Explicit, 0, 0, []);
        builder.DeclareStruct("Padded", "Padded", SourceSpan.None, Origin.Synthetic, [Field("A", i32, -1)], StructLayoutKind.C, 0, 20, []);
        builder.DeclareStruct("Reordered", "Reordered", SourceSpan.None, Origin.Synthetic, [Field("A", i8, -1), Field("B", f64, -1), Field("C", i8, -1), Field("D", i32, -1)], StructLayoutKind.Auto, 0, 0, []);
        TargetTable.TryGet(target, out Target data);
        LayoutPass.Run(module, data);
        module.Target = data;
        module.Stage = ModuleStage.Final;
        string directory = Directory.CreateTempSubdirectory("cdelta-special").FullName;
        try
        {
            string source = Path.Combine(directory, "special.c");
            File.WriteAllText(source, CBackend.Generate(module, new BackendOptions(false, true)).Program);
            var bag = new CDelta.Diagnostics.DiagnosticBag();
            IToolchainProvider? toolchain = ToolchainSelector.Select("auto", target, new ProcessRunner(), Environment.GetEnvironmentVariable, PathSearch.Find, bag);

            ToolResult result = toolchain!.Compile(new CompileRequest(source, Path.Combine(directory, "special.o"), target, OptimizeMode.Debug, [], []));

            Assert.True(result.ExitCode == 0, result.CommandLine + "\n" + result.StandardError);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    private static Module Catalog(string target)
    {
        Module module = CatalogModule.Build(LayoutCatalog.Cases);
        TargetTable.TryGet(target, out Target data);
        LayoutPass.Run(module, data);
        module.Target = data;
        module.Stage = ModuleStage.Final;
        return module;
    }
}
