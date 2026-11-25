using CDelta.Diagnostics;
using CDelta.Ir;
using CDelta.Tests;

namespace CDelta.Core.Tests;

public sealed class CorePipelineTests
{
    private static Module Run(BuildMode mode, SafetyOverride safety = SafetyOverride.Default)
    {
        Module lowered = LoweredPrograms.DivideAndMain(safety);
        TargetTable.TryGet("x86_64-linux-gnu", out Target target);
        var bag = new DiagnosticBag();
        Module? final = CorePipeline.Run(lowered, new DictionaryBodyProvider(lowered), new CoreOptions(mode, target), bag);

        Assert.False(bag.HasError);
        return final!;
    }

    private static void AssertGolden(string name, Module module)
    {
        string path = Path.Combine(GoldenFile.AreaPath("CDelta.Core.Tests", "Passes"), name + ".ir");
        GoldenFile.AssertMatches(path, IrPrinter.Print(module).Replace(GoldenFile.RepositoryRoot(), "$ROOT"));
    }

    [Fact]
    public void DebugTrapsAndChecks() => AssertGolden("debug", Run(BuildMode.Debug));

    [Fact]
    public void ReleaseFastAssumesButStillChecksDivision() => AssertGolden("release-fast", Run(BuildMode.ReleaseFast));

    [Fact]
    public void SafetyOverrideTurnsChecksOffInDebug() => AssertGolden("safety-off", Run(BuildMode.Debug, SafetyOverride.Off));

    [Fact]
    public void OnlyReachableFunctionsRemain()
    {
        Module final = Run(BuildMode.Debug);

        Assert.DoesNotContain(final.Functions, f => final.Text(f.Key) == "Sample.Program.Unused");
        Assert.Contains(final.Functions, f => final.Text(f.Key) == "synthetic:main");
    }

    [Fact]
    public void ResultIsFinalAndVerifies()
    {
        Module final = Run(BuildMode.Debug);

        Assert.Equal(ModuleStage.Final, final.Stage);
        Assert.False(IrVerifier.Verify(final, ModuleStage.Final).HasError);
    }
}
