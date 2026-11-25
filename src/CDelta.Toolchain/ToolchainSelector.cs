using CDelta.Diagnostics;

namespace CDelta.Toolchain;

public static class ToolchainSelector
{
    private const string Auto = "auto";

    public static IToolchainProvider? Select(
        string preference,
        string target,
        IProcessRunner runner,
        Func<string, string?> environment,
        Func<string, string?> findOnPath,
        DiagnosticBag diagnostics)
    {
        var candidates = new List<IToolchainProvider>();
        if (preference is Auto or "zig")
        {
            string? zig = environment("CDELTA_ZIG") ?? findOnPath("zig");
            if (zig is not null)
            {
                candidates.Add(new ZigProvider(runner, zig));
            }
        }

        if (preference is Auto or "clang")
        {
            string? clang = environment("CDELTA_CLANG") ?? findOnPath("clang");
            if (clang is not null)
            {
                candidates.Add(new ClangProvider(runner, clang));
            }
        }

        foreach (IToolchainProvider candidate in candidates)
        {
            if (candidate.SupportsTarget(target) && candidate.MeetsMinimumVersion(out _))
            {
                return candidate;
            }
        }

        diagnostics.Add(
            DiagnosticCode.CD0002,
            new Location("cdc", 0, 0),
            new Note(null, "target " + target + ", toolchain " + preference));
        return null;
    }
}
