using CDelta.Core;
using CDelta.Core.Tests;
using CDelta.Ir;
using CDelta.Tests;
using CDelta.Toolchain;

namespace CDelta.Backend.C.Tests;

public sealed class HeaderTests
{
    private const string Program = """
        module Geometry stage lowered

        source 1 "$ROOT/Geometry.cs" "Geometry.cs"

        #[cdelta.Export]
        fn Geo.Add(a: i32, b: i32) -> i32 export("geo_add") origin source(csharp) @1:1:1 {
          local %0 a: i32 param
          local %1 b: i32 param
          local %2 sum: i32 temp
        bb0:
          %2 = binop add default copy %0, copy %1  @1:1:3
          return copy %2  @1:1:3
        }

        #[cdelta.Export]
        fn Geo.Apply(callback: fn(i32) -> i32, text: ptr<u8> const many, value: i64) -> bool export("geo_apply") origin source(csharp) @1:2:1 {
          local %0 callback: fn(i32) -> i32 param
          local %1 text: ptr<u8> const many param
          local %2 value: i64 param
        bb0:
          return const bool true  @1:2:3
        }

        fn Geo.Hidden(a: i32) -> i32 origin source(csharp) @1:3:1 {
          local %0 a: i32 param
        bb0:
          return copy %0  @1:3:3
        }

        """;

    [Fact]
    public void TheHeaderDeclaresTheExportsOnly()
    {
        IrParseResult parsed = IrParser.Parse(Program);
        Assert.True(parsed.Module is not null, parsed.Error + " (line " + parsed.Line + ")");
        Module final = CompiledProgram.Final(parsed.Module!, BuildMode.Debug);

        CBackendOutput generated = CBackend.Generate(final, new BackendOptions(false, true, false, "geometry"));

        GoldenFile.AssertMatches(Path.Combine(GoldenFile.AreaPath("CDelta.Backend.C.Tests", "Output"), "geometry.h"), generated.Header!);
    }

    [Fact]
    public void NothingExportedWritesNoHeader()
    {
        Module final = CompiledProgram.Final(LoweredPrograms.DivideAndMain(), BuildMode.Debug);

        Assert.Null(CBackend.Generate(final, new BackendOptions(false, true, false, "divide")).Header);
    }
}
