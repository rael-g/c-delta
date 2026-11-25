using CDelta.Core;
using CDelta.Ir;
using CDelta.Toolchain;

namespace CDelta.Backend.C.Tests;

public sealed class DataTests
{
    [Fact]
    public void StructsGlobalsArraysAndPointersRun()
    {
        Module final = CompiledProgram.Final(DataPrograms.Build(2, false), BuildMode.Debug);

        Assert.False(IrVerifier.Verify(final, ModuleStage.Final).HasError);
        Assert.Equal(4 + 4 + 3 + 5 + 4, CompiledProgram.Run(final, OptimizeMode.Debug).ExitCode);
    }

    [Fact]
    public void AnIndexOutOfBoundsPanicsInDebug()
    {
        Module final = CompiledProgram.Final(DataPrograms.Build(4, false), BuildMode.Debug);

        (int exitCode, string error) = CompiledProgram.Run(final, OptimizeMode.Debug);

        Assert.NotEqual(0, exitCode);
        Assert.StartsWith("panic: index out of bounds\n", error);
    }

    [Fact]
    public void ADereferenceOfANullPointerPanicsInDebug()
    {
        Module final = CompiledProgram.Final(DataPrograms.Build(0, true), BuildMode.Debug);

        (int exitCode, string error) = CompiledProgram.Run(final, OptimizeMode.Debug);

        Assert.NotEqual(0, exitCode);
        Assert.StartsWith("panic: null pointer dereference\n", error);
    }

    [Fact]
    public void TheModuleVerifiesBeforeTheCore()
    {
        Module lowered = DataPrograms.Build(2, false);

        Assert.False(IrVerifier.Verify(lowered, ModuleStage.Lowered).HasError);
    }
}
