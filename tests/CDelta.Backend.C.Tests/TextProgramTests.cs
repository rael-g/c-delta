using CDelta.Core;
using CDelta.Ir;
using CDelta.Toolchain;

namespace CDelta.Backend.C.Tests;

public sealed class TextProgramTests
{
    private const string Program = """
        module Text stage lowered

        source 1 "$ROOT/Text.cs" "Text.cs"

        #[cdelta.EntryPoint]
        fn Text.Main() -> i32 origin source(csharp) @1:1:1 {
          local %0 text: slice<u8> const text user
          local %1 offset: i32 user
          local %2 first: i32 temp
          local %3 second: i32 temp
          local %4 third: i32 temp
          local %5 fourth: i32 temp
          local %6 same: bool temp
          local %7 different: bool temp
          local %8 sum: i32 temp
          local %9 cursor: ptr<i32> temp
        bb0:
          %0 = const slice<u8> const text bytes "\xFF\xC3\xA9\xE2\x82"  @1:2:3
          %1 = const i32 0  @1:2:3
          %9 = addr.mut %1  @1:2:3
          %2 = intrinsic utf8.next(copy %0, copy %9)  @1:2:3
          %3 = intrinsic utf8.next(copy %0, copy %9)  @1:2:3
          %4 = intrinsic utf8.next(copy %0, copy %9)  @1:2:3
          %5 = intrinsic utf8.next(copy %0, copy %9)  @1:2:3
          %6 = intrinsic text.eql(copy %0, const slice<u8> const text bytes "\xFF\xC3\xA9\xE2\x82")  @1:2:3
          %7 = intrinsic text.eql(copy %0, const slice<u8> const text bytes "\xFF")  @1:2:3
          %8 = binop sub default copy %2, const i32 65533  @1:2:3
          %8 = binop add default copy %8, copy %3  @1:2:3
          %8 = binop add default copy %8, copy %1  @1:2:3
          return copy %8  @1:2:3
        }

        """;

    [Fact]
    public void InvalidUtf8GivesTheReplacementCharacterAndAdvancesByOne()
    {
        IrParseResult parsed = IrParser.Parse(Program);
        Assert.True(parsed.Module is not null, parsed.Error + " (line " + parsed.Line + ")");
        Module final = CompiledProgram.Final(parsed.Module!, BuildMode.Debug);

        Assert.False(IrVerifier.Verify(final, ModuleStage.Final).HasError);
        Assert.Equal(0 + 233 + 5, CompiledProgram.Run(final, OptimizeMode.Debug).ExitCode);
    }
}
