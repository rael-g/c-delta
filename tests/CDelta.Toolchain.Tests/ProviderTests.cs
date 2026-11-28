using CDelta.Diagnostics;
using CDelta.Toolchain;

namespace CDelta.Toolchain.Tests;

public sealed class ProviderTests
{
    private static ToolResult Ok(string output) => new(0, output, string.Empty, string.Empty);

    [Theory]
    [InlineData("0.15.1", true)]
    [InlineData("0.16.0", true)]
    [InlineData("0.15.0", false)]
    [InlineData("0.14.9", false)]
    public void ZigMinimumVersion(string reported, bool accepted)
    {
        var zig = new ZigProvider(new FakeProcessRunner((_, _) => Ok(reported + "\n")), "zig");

        Assert.Equal(accepted, zig.MeetsMinimumVersion(out string version));
        Assert.Equal(reported, version);
    }

    [Theory]
    [InlineData("Ubuntu clang version 18.1.3 (1ubuntu1)", true)]
    [InlineData("clang version 17.0.6", false)]
    public void ClangMinimumVersion(string banner, bool accepted)
    {
        var clang = new ClangProvider(new FakeProcessRunner((_, _) => Ok(banner + "\n")), "clang");

        Assert.Equal(accepted, clang.MeetsMinimumVersion(out _));
    }

    [Fact]
    public void ZigCompileCommandLine()
    {
        var runner = new FakeProcessRunner((_, _) => Ok(string.Empty));
        var zig = new ZigProvider(runner, "zig");

        zig.Compile(new CompileRequest("a.c", "a.o", "aarch64-linux-gnu", OptimizeMode.Debug, ["inc"], ["X=1"]));

        Assert.Equal(
            ["cc", "-target", "aarch64-linux-gnu", "-c", "-std=c11", "-fno-strict-aliasing", "-ffp-contract=off", "-w", "-O0", "-g", "-fno-sanitize=undefined", "-Iinc", "-DX=1", "a.c", "-o", "a.o"],
            runner.Calls[0].Arguments);
    }

    [Fact]
    public void ZigCompilesAndLinksCppWithItsCppDriver()
    {
        var runner = new FakeProcessRunner((_, _) => Ok(string.Empty));
        var zig = new ZigProvider(runner, "zig");

        zig.Compile(new CompileRequest("a.cpp", "a.o", "x86_64-linux-gnu", OptimizeMode.Debug, [], [], null, true));
        zig.Link(new LinkRequest(["a.o"], "a", "x86_64-linux-gnu", OptimizeMode.Debug, [], null, null, true));

        Assert.Equal("c++", runner.Calls[0].Arguments[0]);
        Assert.DoesNotContain("-std=c11", runner.Calls[0].Arguments);
        Assert.Equal("c++", runner.Calls[1].Arguments[0]);
    }

    [Theory]
    [InlineData("clang", "clang++")]
    [InlineData("/usr/bin/clang", "/usr/bin/clang++")]
    [InlineData("/usr/bin/clang-18", "/usr/bin/clang++-18")]
    public void ClangUsesItsCppDriverForCpp(string executable, string expected)
    {
        var runner = new FakeProcessRunner((_, _) => Ok(string.Empty));
        var clang = new ClangProvider(runner, executable);

        clang.Compile(new CompileRequest("a.cpp", "a.o", "x86_64-linux-gnu", OptimizeMode.Debug, [], [], null, true));
        clang.Compile(new CompileRequest("a.c", "a.o", "x86_64-linux-gnu", OptimizeMode.Debug, [], []));

        Assert.Equal(expected, runner.Calls[0].Executable);
        Assert.Equal(executable, runner.Calls[1].Executable);
    }

    [Fact]
    public void LinkPassesEntryScriptAndLinkerArguments()
    {
        var runner = new FakeProcessRunner((_, _) => Ok(string.Empty));
        var clang = new ClangProvider(runner, "clang");

        clang.Link(new LinkRequest(["a.o"], "a", "x86_64-freestanding-none", OptimizeMode.Debug, [], null, null, false, "reset", "/p/link.ld", ["--gc-sections", "-Map=a.map"]));

        Assert.Equal(
            ["--target=x86_64-unknown-none-elf", "a.o", "-o", "a", "-Wl,--entry=reset", "-T", "/p/link.ld", "-Wl,--gc-sections", "-Wl,-Map=a.map"],
            runner.Calls[0].Arguments);
    }

    [Fact]
    public void AssemblyIsCompiledWithoutTheCStandard()
    {
        var runner = new FakeProcessRunner((_, _) => Ok(string.Empty));
        var zig = new ZigProvider(runner, "zig");

        zig.Compile(new CompileRequest("a.s", "a.o", "x86_64-linux-gnu", OptimizeMode.Debug, [], [], null, false, true));

        Assert.Equal("cc", runner.Calls[0].Arguments[0]);
        Assert.DoesNotContain("-std=c11", runner.Calls[0].Arguments);
        Assert.Contains("a.s", runner.Calls[0].Arguments);
    }

    [Fact]
    public void ClangUsesItsOwnTriple()
    {
        var runner = new FakeProcessRunner((_, _) => Ok(string.Empty));
        var clang = new ClangProvider(runner, "clang");

        clang.Compile(new CompileRequest("a.c", "a.o", "x86_64-windows-gnu", OptimizeMode.ReleaseFast, [], []));

        Assert.Contains("--target=x86_64-w64-windows-gnu", runner.Calls[0].Arguments);
        Assert.Contains("-O3", runner.Calls[0].Arguments);
        Assert.DoesNotContain("-g", runner.Calls[0].Arguments);
    }

    [Fact]
    public void LinkSkipsLibcAndPassesOtherLibraries()
    {
        var runner = new FakeProcessRunner((_, _) => Ok(string.Empty));
        var zig = new ZigProvider(runner, "zig");

        zig.Link(new LinkRequest(["a.o"], "a", "x86_64-linux-gnu", OptimizeMode.Debug, ["c", "m", "z"]));

        Assert.Equal(["cc", "-target", "x86_64-linux-gnu", "a.o", "-o", "a", "-lm", "-lz"], runner.Calls[0].Arguments);
    }

    [Fact]
    public void SelectionPrefersZigAndHonorsTheVersion()
    {
        var runner = new FakeProcessRunner((exe, _) => Ok(exe == "zig" ? "0.16.0" : "clang version 18.1.3"));
        var bag = new DiagnosticBag();

        IToolchainProvider? provider = ToolchainSelector.Select("auto", "x86_64-linux-gnu", runner, _ => null, name => name, bag);

        Assert.Equal("zig", provider!.Name);
        Assert.False(bag.HasError);
    }

    [Fact]
    public void SelectionFallsBackToClangWhenZigIsTooOld()
    {
        var runner = new FakeProcessRunner((exe, _) => Ok(exe == "zig" ? "0.14.0" : "clang version 18.1.3"));
        var bag = new DiagnosticBag();

        IToolchainProvider? provider = ToolchainSelector.Select("auto", "x86_64-linux-gnu", runner, _ => null, name => name, bag);

        Assert.Equal("clang", provider!.Name);
    }

    [Fact]
    public void SelectionReportsCD0002WhenNothingFits()
    {
        var runner = new FakeProcessRunner((_, _) => Ok("0.1.0"));
        var bag = new DiagnosticBag();

        IToolchainProvider? provider = ToolchainSelector.Select("auto", "x86_64-linux-gnu", runner, _ => null, name => name, bag);

        Assert.Null(provider);
        Assert.Contains(bag.Items, d => d.Code == DiagnosticCode.CD0002);
    }

    [Fact]
    public void EnvironmentVariableOverridesThePath()
    {
        var runner = new FakeProcessRunner((_, _) => Ok("0.16.0"));
        var bag = new DiagnosticBag();

        IToolchainProvider? provider = ToolchainSelector.Select(
            "zig",
            "x86_64-linux-gnu",
            runner,
            name => name == "CDELTA_ZIG" ? "/opt/zig" : null,
            name => name,
            bag);

        Assert.Equal("/opt/zig", provider!.Executable);
    }
}
