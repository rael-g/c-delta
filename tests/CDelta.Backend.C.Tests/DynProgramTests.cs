using CDelta.Core;
using CDelta.Ir;
using CDelta.Toolchain;

namespace CDelta.Backend.C.Tests;

public sealed class DynProgramTests
{
    private const string Program = """
        module Dyn stage lowered

        source 1 "$ROOT/Dyn.cs" "Dyn.cs"

        struct Gen.A layout c origin source(csharp) @1:1:1 {
          x: i32
        }

        interface Gen.IGet @1:2:1 {
          fn Get() -> i32
          fn Add(i32) -> i32
        }

        fn Gen.AAdd(this: ref struct Gen.A, n: i32) -> i32 origin source(csharp) @1:4:1 {
          local %0 this: ptr<struct Gen.A> param
          local %1 n: i32 param
        bb0:
          return copy %1  @1:4:3
        }

        fn Gen.AGet(this: ref struct Gen.A) -> i32 origin source(csharp) @1:3:1 {
          local %0 this: ptr<struct Gen.A> param
        bb0:
          return const i32 40  @1:3:3
        }

        #[cdelta.EntryPoint]
        fn Gen.Main() -> i32 origin source(csharp) @1:5:1 {
          local %0 a: struct Gen.A user
          local %1 p: ptr<struct Gen.A> temp
          local %2 d: dyn<interface Gen.IGet> temp
          local %3 first: i32 temp
          local %4 second: i32 temp
          local %5 sum: i32 temp
        bb0:
          %1 = addr.mut %0  @1:5:3
          %2 = call direct Gen.Wrap<struct Gen.A>(copy %1)  @1:5:3
          %3 = call dyn copy %2 0()  @1:5:4
          %4 = call dyn copy %2 1(const i32 2)  @1:5:5
          %5 = binop add default copy %3, copy %4  @1:5:6
          return copy %5  @1:5:7
        }

        fn Gen.Wrap<type T>(p: ptr<param T>) -> dyn<interface Gen.IGet> origin source(csharp) @1:6:1 {
          local %0 p: ptr<param T> param
          local %1 d: dyn<interface Gen.IGet> temp
        bb0:
          %1 = dyn.make param T interface Gen.IGet copy %0  @1:6:3
          return copy %1  @1:6:3
        }

        impl Gen.ImplA = struct Gen.A : interface Gen.IGet {
          Add = Gen.AAdd
          Get = Gen.AGet
        }

        """;

    [Fact]
    public void ADynCallGoesThroughTheTableOfItsImpl()
    {
        IrParseResult parsed = IrParser.Parse(Program);
        Assert.True(parsed.Module is not null, parsed.Error + " (line " + parsed.Line + ")");
        Assert.False(IrVerifier.Verify(parsed.Module!, ModuleStage.Lowered).HasError);

        Module final = CompiledProgram.Final(parsed.Module!, BuildMode.Debug);

        Assert.False(IrVerifier.Verify(final, ModuleStage.Final).HasError);
        Assert.Single(final.Impls);
        Assert.Equal(42, CompiledProgram.Run(final, OptimizeMode.Debug).ExitCode);
    }
}
