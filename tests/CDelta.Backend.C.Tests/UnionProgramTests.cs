using CDelta.Core;
using CDelta.Ir;
using CDelta.Toolchain;

namespace CDelta.Backend.C.Tests;

public sealed class UnionProgramTests
{
    private const string Program = """
        module Shapes stage lowered

        source 1 "$ROOT/Shapes.cs" "Shapes.cs"

        struct Gen.A layout c origin source(csharp) @1:1:1 {
          x: i32
        }

        struct Gen.B layout c origin source(csharp) @1:2:1 {
          y: i64
        }

        union Gen.U of interface Gen.IShape @1:3:1 {
          struct Gen.A
          struct Gen.B
        }

        interface Gen.IShape @1:5:1 {
          fn Get() -> i32
        }

        #[cdelta.EntryPoint]
        fn Gen.Main() -> i32 origin source(csharp) @1:4:1 {
          local %0 a: struct Gen.A temp
          local %1 b: struct Gen.B temp
          local %2 u: union Gen.U temp
          local %3 v: union Gen.U temp
          local %4 t: u32 temp
          local %5 c: bool temp
          local %6 r: i32 temp
          local %7 s: i32 temp
          local %8 w: u32 temp
          local %9 d: bool temp
        bb0:
          %0 = aggregate struct Gen.A(const i32 40)  @1:4:3
          %2 = union.make union Gen.U 0 copy %0  @1:4:3
          %1 = aggregate struct Gen.B(const i64 2)  @1:4:3
          %3 = union.make union Gen.U 1 copy %1  @1:4:3
          %4 = tag %2  @1:4:3
          %5 = binop eq copy %4, const u32 0  @1:4:3
          branch copy %5 ? bb1 : bb4  @1:4:3
        bb1:
          %6 = copy %2.payload(0).field(0)  @1:4:3
          goto bb2  @1:4:3
        bb2:
          %8 = tag %3  @1:4:3
          %9 = binop eq copy %8, const u32 1  @1:4:3
          branch copy %9 ? bb3 : bb4  @1:4:3
        bb3:
          %7 = cast int.int wrap copy %3.payload(1).field(0) to i32  @1:4:3
          %6 = binop add default copy %6, copy %7  @1:4:3
          return copy %6  @1:4:3
        bb4:
          return const i32 0  @1:4:3
        }

        """;

    private const string Calls = """
        module Shapes stage lowered

        source 1 "$ROOT/Shapes.cs" "Shapes.cs"

        struct Gen.A layout c origin source(csharp) @1:1:1 {
          x: i32
        }

        struct Gen.B layout c origin source(csharp) @1:2:1 {
          y: i64
        }

        union Gen.U of interface Gen.IShape @1:3:1 {
          struct Gen.A
          struct Gen.B
        }

        interface Gen.IShape @1:5:1 {
          fn Get(i32) -> i32
        }

        fn Gen.AGet(this: ptr<struct Gen.A>, n: i32) -> i32 origin source(csharp) @1:6:1 {
          local %0 this: ptr<struct Gen.A> param
          local %1 n: i32 param
          local %2 r: i32 temp
        bb0:
          %2 = binop add default copy (*%0).field(0), copy %1  @1:6:3
          return copy %2  @1:6:3
        }

        fn Gen.BGet(this: ptr<struct Gen.B>, n: i32) -> i32 origin source(csharp) @1:7:1 {
          local %0 this: ptr<struct Gen.B> param
          local %1 n: i32 param
          local %2 w: i32 temp
          local %3 r: i32 temp
        bb0:
          %2 = cast int.int wrap copy (*%0).field(0) to i32  @1:7:3
          %3 = binop add default copy %2, copy %1  @1:7:3
          return copy %3  @1:7:3
        }

        #[cdelta.EntryPoint]
        fn Gen.Main() -> i32 origin source(csharp) @1:4:1 {
          local %0 a: struct Gen.A temp
          local %1 b: struct Gen.B temp
          local %2 u: union Gen.U temp
          local %3 v: union Gen.U temp
          local %4 pu: ptr<union Gen.U> temp
          local %5 pv: ptr<union Gen.U> temp
          local %6 first: i32 temp
          local %7 second: i32 temp
          local %8 sum: i32 temp
        bb0:
          %0 = aggregate struct Gen.A(const i32 30)  @1:4:3
          %2 = union.make union Gen.U 0 copy %0  @1:4:3
          %1 = aggregate struct Gen.B(const i64 10)  @1:4:3
          %3 = union.make union Gen.U 1 copy %1  @1:4:3
          %4 = addr.mut %2  @1:4:3
          %5 = addr.mut %3  @1:4:3
          %6 = call union copy %4 0(const i32 1)  @1:4:3
          %7 = call union copy %5 0(const i32 1)  @1:4:3
          %8 = binop add default copy %6, copy %7  @1:4:3
          return copy %8  @1:4:3
        }

        impl Gen.ImplA = struct Gen.A : interface Gen.IShape {
          Get = Gen.AGet
        }

        impl Gen.ImplB = struct Gen.B : interface Gen.IShape {
          Get = Gen.BGet
        }

        """;

    [Fact]
    public void ACallOnAUnionSwitchesOnTheTagAndCallsTheImplOfItsMember()
    {
        IrParseResult parsed = IrParser.Parse(Calls);
        Assert.True(parsed.Module is not null, parsed.Error + " (line " + parsed.Line + ")");
        Assert.False(IrVerifier.Verify(parsed.Module!, ModuleStage.Lowered).HasError);

        Module final = CompiledProgram.Final(parsed.Module!, BuildMode.Debug);

        Assert.False(IrVerifier.Verify(final, ModuleStage.Final).HasError);
        Assert.Equal(42, CompiledProgram.Run(final, OptimizeMode.Debug).ExitCode);
    }

    [Fact]
    public void AUnionHoldsOneMemberAndItsTagSelectsIt()
    {
        IrParseResult parsed = IrParser.Parse(Program);
        Assert.True(parsed.Module is not null, parsed.Error + " (line " + parsed.Line + ")");
        Assert.False(IrVerifier.Verify(parsed.Module!, ModuleStage.Lowered).HasError);

        Module final = CompiledProgram.Final(parsed.Module!, BuildMode.Debug);

        Assert.False(IrVerifier.Verify(final, ModuleStage.Final).HasError);
        Assert.Equal(16, final.Unions[0].Size);
        Assert.Equal(42, CompiledProgram.Run(final, OptimizeMode.Debug).ExitCode);
    }
}
