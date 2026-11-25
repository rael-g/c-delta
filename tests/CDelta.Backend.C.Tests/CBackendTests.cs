using CDelta.Core;
using CDelta.Core.Tests;
using CDelta.Ir;
using CDelta.Tests;
using CDelta.Toolchain;

namespace CDelta.Backend.C.Tests;

public sealed class CBackendTests
{
    private static void AssertGolden(string name, string text) =>
        GoldenFile.AssertMatches(Path.Combine(GoldenFile.AreaPath("CDelta.Backend.C.Tests", "Output"), name + ".c"), text);

    [Fact]
    public void DebugOutputWithLineDirectives()
    {
        Module final = CompiledProgram.Final(LoweredPrograms.DivideAndMain(), BuildMode.Debug);

        AssertGolden("divide-debug", CompiledProgram.Generate(final, true));
    }

    [Fact]
    public void ReleaseFastOutputHasNoLineDirectives()
    {
        Module final = CompiledProgram.Final(LoweredPrograms.DivideAndMain(), BuildMode.ReleaseFast);

        AssertGolden("divide-release-fast", CompiledProgram.Generate(final, false));
    }

    [Fact]
    public void ProgramRunsAndReturnsItsExitCode()
    {
        Module final = CompiledProgram.Final(LoweredPrograms.DivideAndMain(), BuildMode.Debug);

        Assert.Equal(4, CompiledProgram.Run(final, OptimizeMode.Debug).ExitCode);
    }

    [Fact]
    public void DivisionByZeroPanics()
    {
        Module final = CompiledProgram.Final(LoweredPrograms.DivideAndMain(divisor: 0), BuildMode.ReleaseFast);

        (int exitCode, string error) = CompiledProgram.Run(final, OptimizeMode.ReleaseFast);

        Assert.NotEqual(0, exitCode);
        Assert.StartsWith("panic: division by zero\n  at Program.cs:3\n", error);
    }

    [Fact]
    public void OverflowPanicsInDebug()
    {
        Module final = CompiledProgram.Final(LoweredPrograms.AddOverflow(ArithMode.Default), BuildMode.Debug);

        (int exitCode, string error) = CompiledProgram.Run(final, OptimizeMode.Debug);

        Assert.NotEqual(0, exitCode);
        Assert.StartsWith("panic: integer overflow\n  at Overflow.cs:5\n", error);
    }

    [Fact]
    public void OverflowWrapsWhenUnchecked()
    {
        Module final = CompiledProgram.Final(LoweredPrograms.AddOverflow(ArithMode.Wrap), BuildMode.Debug);

        Assert.Equal(0, CompiledProgram.Run(final, OptimizeMode.Debug).ExitCode);
    }

    [Fact]
    public void CheckedOverflowPanicsInEveryMode()
    {
        Module final = CompiledProgram.Final(LoweredPrograms.AddOverflow(ArithMode.Trap), BuildMode.ReleaseFast);

        (int exitCode, string error) = CompiledProgram.Run(final, OptimizeMode.ReleaseFast);

        Assert.NotEqual(0, exitCode);
        Assert.StartsWith("panic: integer overflow", error);
    }
}
