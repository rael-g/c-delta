using CDelta.Core;
using CDelta.Ir;
using CDelta.Toolchain;

namespace CDelta.Backend.C.Tests;

public sealed class SliceProgramTests
{
    private const string Template = """
        module Slices stage lowered

        source 1 "$ROOT/Slices.cs" "Slices.cs"

        #[cdelta.EntryPoint]
        fn Slices.Main() -> i32 origin source(csharp) @1:1:1 {
          local %0 buf: array<i32, 4> user
          local %1 view: slice<i32> user
          local %2 n: i32 temp
          local %3 picked: i32 temp
          local %4 opt: optional<i32> user
          local %5 present: bool temp
          local %6 payload: i32 temp
          local %7 stack: ptr<i32> nullable many temp
          local %8 next: ptr<i32> nullable many temp
          local %9 zero: i32 temp
          local %10 sum: i32 temp
          local %11 view_len: i32 temp
          local %12 total: i32 temp
        bb0:
          %0 = aggregate array<i32, 4>(const i32 10, const i32 20, const i32 30, const i32 40)  @1:2:3
          %1 = slice %0, const i32 1, const i32 __END__  @1:2:3
          %2 = len %1  @1:2:3
          %3 = copy %1[const i32 1]  @1:2:3
          %4 = optional.some copy %2  @1:2:3
          %5 = haspayload %4  @1:2:3
          %6 = copy %4.payload(0)  @1:2:3
          %7 = alloca i32, const usize 3, zero  @1:2:3
          %8 = binop add wrap copy %7, const isize 1  @1:2:3
          %9 = copy (*%8)  @1:2:3
          %10 = binop add default copy %3, copy %6  @1:2:3
          %12 = binop add default copy %10, copy %9  @1:2:3
          return copy %12  @1:2:3
        }

        """;

    [Fact]
    public void SlicesOptionalsAndAllocaRun()
    {
        Module final = Final(3);

        Assert.False(IrVerifier.Verify(final, ModuleStage.Final).HasError);
        Assert.Equal(30 + 2 + 0, CompiledProgram.Run(final, OptimizeMode.Debug).ExitCode);
    }

    [Fact]
    public void ASliceBeyondTheArrayPanicsInDebug()
    {
        (int exitCode, string error) = CompiledProgram.Run(Final(9), OptimizeMode.Debug);

        Assert.NotEqual(0, exitCode);
        Assert.StartsWith("panic: slice out of bounds\n", error);
    }

    private static Module Final(int end)
    {
        IrParseResult parsed = IrParser.Parse(Template.Replace("__END__", end.ToString()));
        Assert.True(parsed.Module is not null, parsed.Error + " (line " + parsed.Line + ")");
        return CompiledProgram.Final(parsed.Module!, BuildMode.Debug);
    }
}
