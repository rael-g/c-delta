using CDelta.Driver;
using CDelta.Toolchain;

namespace CDelta.Driver.Tests;

public sealed class PipelineTests
{
    private static BuildResult Build(string source, string target = "x86_64-linux-gnu", string toolchain = "auto")
    {
        string directory = Directory.CreateTempSubdirectory("cdelta-driver").FullName;
        string path = Path.Combine(directory, "Program.cs");
        File.WriteAllText(path, source);
        var options = new CompilerOptions([path], target, "Debug", Path.Combine(directory, "out"), toolchain, false, null, false, false);
        return new BuildPipeline(new ProcessRunner(), Environment.GetEnvironmentVariable).Build(options);
    }

    [Fact]
    public void ABindingErrorFailsTheBuild()
    {
        BuildResult result = Build("static class P { static int Main() { return missing; } }");

        Assert.Equal(1, result.ExitCode);
        Assert.Contains(result.Messages, m => m.Contains("error CS0103", StringComparison.Ordinal));
    }

    [Fact]
    public void ANoMainProgramReportsCD0007()
    {
        BuildResult result = Build("static class P { }");

        Assert.Equal(1, result.ExitCode);
        Assert.Contains(result.Messages, m => m.Contains("error CD0007: an executable has no 'Main'", StringComparison.Ordinal));
    }

    [Fact]
    public void AnUnsupportedTargetReportsCD0006()
    {
        BuildResult result = Build("static class P { static void Main() { } }", "riscv64-linux-gnu");

        Assert.Equal(1, result.ExitCode);
        Assert.Contains(result.Messages, m => m.Contains("error CD0006", StringComparison.Ordinal));
    }

    [Fact]
    public void ABuildProducesAnExecutable()
    {
        BuildResult result = Build("static class P { static void Main() { } }");

        Assert.Equal(0, result.ExitCode);
        Assert.True(File.Exists(result.OutputPath));
    }

    [Fact]
    public void TheClangProviderBuildsToo()
    {
        BuildResult result = Build("static class P { static int Main() { return 3; } }", toolchain: "clang");

        Assert.Equal(0, result.ExitCode);
        Assert.True(File.Exists(result.OutputPath));
    }

    [Fact]
    public void NativeResolvesToTheHost()
    {
        Assert.NotNull(HostTarget.Resolve());
    }
}
