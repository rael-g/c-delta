namespace CDelta.Cli;

internal static class CommandLineParser
{
    private const string ProgramArgumentsSeparator = "--";
    private const string DefaultTarget = "native";
    private const string DefaultMode = "Debug";
    private const string DefaultToolchain = "auto";

    public static CommandLine? Parse(string[] args)
    {
        if (args.Length == 0 || args[0] is not ("build" or "run"))
        {
            return null;
        }

        var files = new List<string>();
        var programArguments = new List<string>();
        string target = DefaultTarget;
        string mode = DefaultMode;
        string? output = null;
        string toolchain = DefaultToolchain;
        string? dumpIr = null;
        bool emitC = false;
        bool verbose = false;
        bool time = false;

        for (int i = 1; i < args.Length; i++)
        {
            string argument = args[i];
            switch (argument)
            {
                case ProgramArgumentsSeparator:
                    programArguments.AddRange(args[(i + 1)..]);
                    i = args.Length;
                    break;
                case "--emit-c":
                    emitC = true;
                    break;
                case "--verbose":
                    verbose = true;
                    break;
                case "--time":
                    time = true;
                    break;
                case "--target" or "--mode" or "-o" or "--dump-ir" or "--toolchain" or "--option" or "--color":
                    if (i + 1 >= args.Length)
                    {
                        return null;
                    }

                    string value = args[++i];
                    switch (argument)
                    {
                        case "--target":
                            target = value;
                            break;
                        case "--mode":
                            mode = value;
                            break;
                        case "-o":
                            output = value;
                            break;
                        case "--dump-ir":
                            dumpIr = value;
                            break;
                        case "--toolchain":
                            toolchain = value;
                            break;
                    }

                    break;
                default:
                    if (argument.StartsWith('-'))
                    {
                        return null;
                    }

                    files.Add(argument);
                    break;
            }
        }

        if (files.Count == 0)
        {
            return null;
        }

        return new CommandLine(args[0], files, programArguments, target, mode, output, toolchain, emitC, dumpIr, verbose, time);
    }
}
