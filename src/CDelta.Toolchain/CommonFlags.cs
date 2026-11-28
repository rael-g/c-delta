namespace CDelta.Toolchain;

internal static class CommonFlags
{
    public static void AddCompileFlags(List<string> arguments, OptimizeMode mode, bool cStandard = true)
    {
        if (cStandard)
        {
            arguments.Add("-std=c11");
        }

        arguments.Add("-fno-strict-aliasing");
        arguments.Add("-ffp-contract=off");
        arguments.Add("-w");
        switch (mode)
        {
            case OptimizeMode.Debug:
                arguments.Add("-O0");
                arguments.Add("-g");
                break;
            case OptimizeMode.ReleaseSafe:
                arguments.Add("-O2");
                arguments.Add("-g");
                break;
            case OptimizeMode.ReleaseFast:
                arguments.Add("-O3");
                break;
            default:
                arguments.Add("-Os");
                break;
        }
    }

    public static void AddLinkerOptions(List<string> arguments, LinkRequest request)
    {
        if (request.EntrySymbol is { Length: > 0 } entry)
        {
            arguments.Add("-Wl,--entry=" + entry);
        }

        if (request.LinkerScript is { Length: > 0 } script)
        {
            arguments.Add("-T");
            arguments.Add(script);
        }

        foreach (string argument in request.LinkerArgs ?? [])
        {
            arguments.Add("-Wl," + argument);
        }
    }

    public static void AddSharedCompileFlags(List<string> arguments, CompileRequest request)
    {
        if (request.SharedLibrary)
        {
            arguments.Add("-fPIC");
            arguments.Add("-fvisibility=hidden");
        }
    }

    public static void AddLibraries(
        List<string> arguments,
        IReadOnlyList<string> libraries,
        IReadOnlyList<NativeLibrary> nativeLibraries,
        IReadOnlyList<string> directories)
    {
        foreach (string directory in directories)
        {
            arguments.Add("-L" + directory);
        }

        foreach (NativeLibrary native in nativeLibraries)
        {
            arguments.Add(native.Static ? "-l:lib" + native.Name + ".a" : "-l" + native.Name);
        }

        foreach (string library in libraries)
        {
            if (library != "c")
            {
                arguments.Add("-l" + library);
            }
        }
    }
}
