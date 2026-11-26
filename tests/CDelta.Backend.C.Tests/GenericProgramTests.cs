using CDelta.Core;
using CDelta.Diagnostics;
using CDelta.Ir;
using CDelta.Toolchain;

namespace CDelta.Backend.C.Tests;

public sealed class GenericProgramTests
{
    private const string Program = """
        module Generic stage lowered

        source 1 "$ROOT/Generic.cs" "Generic.cs"

        fn Generic.Pick<type T>(a: param T, b: param T) -> param T origin source(csharp) @1:1:1 {
          local %0 a: param T param
          local %1 b: param T param
        bb0:
          return copy %1  @1:1:1
        }

        fn Generic.Twice<type T>(x: param T) -> param T origin source(csharp) @1:2:1 {
          local %0 x: param T param
          local %1 r: param T temp
        bb0:
          %1 = call direct Generic.Pick<param T>(copy %0, copy %0)  @1:2:3
          return copy %1  @1:2:3
        }

        #[cdelta.EntryPoint]
        fn Generic.Main() -> i32 origin source(csharp) @1:3:1 {
          local %0 small: i32 temp
          local %1 wide: i64 temp
          local %2 narrowed: i32 temp
          local %3 sum: i32 temp
        bb0:
          %0 = call direct Generic.Twice<i32>(const i32 30)  @1:3:3
          %1 = call direct Generic.Twice<i64>(const i64 12)  @1:3:3
          %2 = cast int.int wrap copy %1 to i32  @1:3:3
          %3 = binop add default copy %0, copy %2  @1:3:3
          return copy %3  @1:3:3
        }

        """;

    [Fact]
    public void EachSetOfTypeArgumentsGetsItsOwnCopy()
    {
        IrParseResult parsed = IrParser.Parse(Program);
        Assert.True(parsed.Module is not null, parsed.Error + " (line " + parsed.Line + ")");
        Assert.False(IrVerifier.Verify(parsed.Module!, ModuleStage.Lowered).HasError);

        Module final = CompiledProgram.Final(parsed.Module!, BuildMode.Debug);

        Assert.False(IrVerifier.Verify(final, ModuleStage.Final).HasError);
        Assert.Equal(4, final.Functions.Count(f => final.Text(f.Key).StartsWith("Generic.Pick<") || final.Text(f.Key).StartsWith("Generic.Twice<")));
        Assert.Equal(42, CompiledProgram.Run(final, OptimizeMode.Debug).ExitCode);
    }

    private const string Boxes = """
        module Boxes stage lowered

        source 1 "$ROOT/Boxes.cs" "Boxes.cs"

        struct Boxes.Box<type T> layout c origin source(csharp) @1:1:1 {
          value: param T
        }

        fn Boxes.Make<type T>(v: param T) -> struct Boxes.Box<param T> origin source(csharp) @1:2:1 {
          local %0 v: param T param
          local %1 b: struct Boxes.Box<param T> temp
        bb0:
          %1 = aggregate struct Boxes.Box<param T>(copy %0)  @1:2:3
          return copy %1  @1:2:3
        }

        #[cdelta.EntryPoint]
        fn Boxes.Main() -> i32 origin source(csharp) @1:3:1 {
          local %0 small: struct Boxes.Box<i32> temp
          local %1 wide: struct Boxes.Box<i64> temp
          local %2 narrowed: i32 temp
          local %3 sum: i32 temp
        bb0:
          %0 = call direct Boxes.Make<i32>(const i32 30)  @1:3:3
          %1 = call direct Boxes.Make<i64>(const i64 12)  @1:3:3
          %2 = cast int.int wrap copy %1.field(0) to i32  @1:3:3
          %3 = binop add default copy %0.field(0), copy %2  @1:3:3
          return copy %3  @1:3:3
        }

        """;

    [Fact]
    public void GenericStructsGetOneLayoutPerInstance()
    {
        IrParseResult parsed = IrParser.Parse(Boxes);
        Assert.True(parsed.Module is not null, parsed.Error + " (line " + parsed.Line + ")");
        Assert.False(IrVerifier.Verify(parsed.Module!, ModuleStage.Lowered).HasError);

        Module final = CompiledProgram.Final(parsed.Module!, BuildMode.Debug);

        Assert.False(IrVerifier.Verify(final, ModuleStage.Final).HasError);
        Assert.Equal(2, final.Structs.Count);
        Assert.Equal(42, CompiledProgram.Run(final, OptimizeMode.Debug).ExitCode);
    }

    private const string Constrained = """
        module Constrained stage lowered

        source 1 "$ROOT/Constrained.cs" "Constrained.cs"

        struct Gen.A layout c origin source(csharp) @1:1:1 {
          x: i32
        }

        struct Gen.B layout c origin source(csharp) @1:2:1 {
          x: i32
        }

        interface Gen.IValue<type R> @1:3:1 {
          static fn Value() -> param R
        }

        fn Gen.AValue() -> i32 origin source(csharp) @1:4:1 {
        bb0:
          return const i32 40  @1:4:3
        }

        fn Gen.BValue() -> i32 origin source(csharp) @1:5:1 {
        bb0:
          return const i32 2  @1:5:3
        }

        fn Gen.Read<type T>() -> i32 origin source(csharp) @1:6:1 {
          local %0 r: i32 temp
        bb0:
          %0 = call constrained param T interface Gen.IValue<i32> Value()  @1:6:3
          return copy %0  @1:6:3
        }

        #[cdelta.EntryPoint]
        fn Gen.Main() -> i32 origin source(csharp) @1:7:1 {
          local %0 a: i32 temp
          local %1 b: i32 temp
          local %2 sum: i32 temp
        bb0:
          %0 = call direct Gen.Read<struct Gen.A>()  @1:7:3
          %1 = call direct Gen.Read<struct Gen.B>()  @1:7:3
          %2 = binop add default copy %0, copy %1  @1:7:3
          return copy %2  @1:7:3
        }

        impl Gen.ImplA = struct Gen.A : interface Gen.IValue<i32> {
          Value = Gen.AValue
        }

        impl Gen.ImplB = struct Gen.B : interface Gen.IValue<i32> {
          Value = Gen.BValue
        }

        """;

    [Fact]
    public void ConstrainedCallsResolveThroughTheImplOfTheConcreteType()
    {
        IrParseResult parsed = IrParser.Parse(Constrained);
        Assert.True(parsed.Module is not null, parsed.Error + " (line " + parsed.Line + ")");
        Assert.False(IrVerifier.Verify(parsed.Module!, ModuleStage.Lowered).HasError);

        Module final = CompiledProgram.Final(parsed.Module!, BuildMode.Debug);

        Assert.False(IrVerifier.Verify(final, ModuleStage.Final).HasError);
        Assert.Equal(42, CompiledProgram.Run(final, OptimizeMode.Debug).ExitCode);
    }

    private const string Runaway = """
        module Runaway stage lowered

        source 1 "$ROOT/Runaway.cs" "Runaway.cs"

        fn Runaway.Grow<type T>(x: param T) -> i32 origin source(csharp) @1:1:1 {
          local %0 x: param T param
          local %1 none: optional<param T> temp
          local %2 r: i32 temp
        bb0:
          %1 = const optional<param T> null  @1:1:3
          %2 = call direct Runaway.Grow<optional<param T>>(copy %1)  @1:1:3
          return copy %2  @1:1:3
        }

        #[cdelta.EntryPoint]
        fn Runaway.Main() -> i32 origin source(csharp) @1:2:1 {
          local %0 r: i32 temp
        bb0:
          %0 = call direct Runaway.Grow<i32>(const i32 1)  @1:2:3
          return copy %0  @1:2:3
        }

        """;

    [Fact]
    public void UnboundedInstantiationIsCD5005()
    {
        IrParseResult parsed = IrParser.Parse(Runaway);
        Assert.True(parsed.Module is not null, parsed.Error + " (line " + parsed.Line + ")");
        Assert.False(IrVerifier.Verify(parsed.Module!, ModuleStage.Lowered).HasError);

        var bag = new DiagnosticBag();
        TargetTable.TryGet("x86_64-linux-gnu", out Target target);
        Module? final = CorePipeline.Run(parsed.Module!, new ModuleBodies(parsed.Module!), new CoreOptions(BuildMode.Debug, target), bag);

        Assert.Null(final);
        Assert.Contains(bag.Items, d => d.Code == DiagnosticCode.CD5005);
    }

    private sealed class ModuleBodies(Module module) : IBodyProvider
    {
        public Body? Lower(FunctionId function) => module.Bodies[function.Index];
    }
}
