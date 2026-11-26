namespace CDelta.Toolchain;

internal static class CommonFlags
{
    public static void AddCompileFlags(List<string> arguments, OptimizeMode mode)
    {
        arguments.Add("-std=c11");
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
