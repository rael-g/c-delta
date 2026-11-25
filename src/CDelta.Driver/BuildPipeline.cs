using CDelta.Backend.C;
using CDelta.Core;
using CDelta.Diagnostics;
using CDelta.Frontend.CSharp;
using CDelta.Frontend.CSharp.Rules;
using CDelta.Ir;
using CDelta.Toolchain;

namespace CDelta.Driver;

public sealed class BuildPipeline(IProcessRunner runner, Func<string, string?> environment)
{
    private const int Success = 0;
    private const int CompileErrors = 1;
    private const int InternalError = 3;
    private const string NativeTarget = "native";
    private const string WindowsMarker = "-windows-";

    public BuildResult Build(CompilerOptions options)
    {
        var messages = new List<string>();
        var diagnostics = new DiagnosticBag();
        try
        {
            string? output = Run(options, diagnostics, messages);
            return Finish(diagnostics, messages, output);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            diagnostics.Add(DiagnosticCode.CD9001, new Location("cdc", 0, 0), new Note(null, exception.ToString()));
            BuildResult result = Finish(diagnostics, messages, null);
            return result with { ExitCode = InternalError };
        }
    }

    private static BuildResult Finish(DiagnosticBag diagnostics, List<string> messages, string? output)
    {
        foreach (CDelta.Diagnostics.Diagnostic item in diagnostics.Items)
        {
            messages.Add(DiagnosticFormatter.Format(item));
        }

        bool failed = diagnostics.HasError || output is null;
        return new BuildResult(failed ? CompileErrors : Success, failed ? null : output, messages);
    }

    private string? Run(CompilerOptions options, DiagnosticBag diagnostics, List<string> messages)
    {
        string? target = ResolveTarget(options.Target);
        if (target is null || !TargetTable.TryGet(target, out Target targetData))
        {
            diagnostics.Add(DiagnosticCode.CD0006, new Location("cdc", 0, 0), new Note(null, "target " + options.Target));
            return null;
        }

        if (!TryParseMode(options.Mode, out BuildMode mode))
        {
            diagnostics.Add(DiagnosticCode.CD0001, new Location("cdc", 0, 0), new Note(null, "unknown mode " + options.Mode));
            return null;
        }

        IToolchainProvider? toolchain = ToolchainSelector.Select(options.Toolchain, target, runner, environment, PathSearch.Find, diagnostics);
        if (toolchain is null)
        {
            return null;
        }

        List<SourceInput> sources = ReadSources(options, diagnostics);
        if (diagnostics.HasError)
        {
            return null;
        }

        string packageName = Path.GetFileNameWithoutExtension(sources[0].FullPath);
        var frontend = CSharpFrontend.Create(sources, LibraryLocator.Corelib(environment), packageName, BuildSymbols.For(target, options.Mode), SubsetAnalyzers.Create(), diagnostics);
        if (frontend.BindingErrors.Count > 0)
        {
            messages.AddRange(frontend.BindingErrors);
            return null;
        }

        IReadOnlyList<FunctionId> roots = frontend.DeclareRoots();
        if (roots.Count == 0)
        {
            diagnostics.Add(DiagnosticCode.CD0007, new Location(sources[0].FullPath, 1, 1));
            return null;
        }

        var bodies = new CachingBodyProvider(frontend, frontend.Builder);
        Module? final = CorePipeline.Run(frontend.Module, bodies, new CoreOptions(mode, targetData), diagnostics);
        string intermediate = BuildPaths.IntermediateForFiles(environment, sources.ConvertAll(s => s.FullPath));
        Directory.CreateDirectory(intermediate);
        DumpIr(options, intermediate, frontend.Module, final);
        if (final is null)
        {
            return null;
        }

        return Native(options, diagnostics, toolchain, final, target, mode, intermediate, packageName, messages);
    }

    private string? Native(
        CompilerOptions options,
        DiagnosticBag diagnostics,
        IToolchainProvider toolchain,
        Module final,
        string target,
        BuildMode mode,
        string intermediate,
        string packageName,
        List<string> messages)
    {
        bool debugInfo = mode is BuildMode.Debug or BuildMode.ReleaseSafe;
        CBackendOutput generated = CBackend.Generate(final, new BackendOptions(debugInfo, true));
        string cPath = Path.Combine(intermediate, packageName + ".c");
        File.WriteAllText(cPath, generated.Program);
        var sources = new List<string> { cPath };
        if (generated.Libc is not null)
        {
            string libcPath = Path.Combine(intermediate, packageName + ".libc.c");
            File.WriteAllText(libcPath, generated.Libc);
            sources.Add(libcPath);
        }

        string objects = Path.Combine(intermediate, "objects");
        Directory.CreateDirectory(objects);
        OptimizeMode optimize = Enum.Parse<OptimizeMode>(mode.ToString());
        var objectPaths = new List<string>();
        foreach (string source in sources)
        {
            string objectPath = Path.Combine(objects, Path.GetFileNameWithoutExtension(source) + ".o");
            ToolResult compiled = toolchain.Compile(new CompileRequest(source, objectPath, target, optimize, [], []));
            Trace(options, compiled, messages);
            if (compiled.ExitCode != 0)
            {
                diagnostics.Add(DiagnosticCode.CD0003, new Location(source, 0, 0), new Note(null, compiled.StandardError.Trim()));
                return null;
            }

            objectPaths.Add(objectPath);
        }

        string output = options.OutputPath ?? DefaultOutput(packageName, target);
        ToolResult linked = toolchain.Link(new LinkRequest(objectPaths, output, target, optimize, LibrariesOf(final)));
        Trace(options, linked, messages);
        if (linked.ExitCode != 0)
        {
            diagnostics.Add(DiagnosticCode.CD0005, new Location(output, 0, 0), new Note(null, linked.StandardError.Trim()));
            return null;
        }

        if (!options.EmitC)
        {
            foreach (string source in sources)
            {
                File.Delete(source);
            }
        }

        return output;
    }

    private static void Trace(CompilerOptions options, ToolResult result, List<string> messages)
    {
        if (options.Verbose)
        {
            messages.Add(result.CommandLine);
        }
    }

    private static string DefaultOutput(string packageName, string target) =>
        Path.Combine(Directory.GetCurrentDirectory(), packageName + (target.Contains(WindowsMarker, StringComparison.Ordinal) ? ".exe" : string.Empty));

    private static List<string> LibrariesOf(Module module)
    {
        var libraries = new SortedSet<string>(StringComparer.Ordinal);
        foreach (FunctionDecl function in module.Functions)
        {
            if (function.Origin.Kind == OriginKind.CLibrary)
            {
                libraries.Add(module.Text(function.Origin.Library));
            }
        }

        return [.. libraries];
    }

    private static void DumpIr(CompilerOptions options, string intermediate, Module lowered, Module? final)
    {
        if (options.DumpIrStage is null)
        {
            return;
        }

        string directory = Path.Combine(intermediate, "ir");
        Directory.CreateDirectory(directory);
        Module? chosen = options.DumpIrStage switch
        {
            "lowered" => lowered,
            "final" => final,
            _ => null,
        };
        if (chosen is not null)
        {
            File.WriteAllText(Path.Combine(directory, options.DumpIrStage + ".ir"), IrPrinter.Print(chosen));
        }
    }

    private string? ResolveTarget(string requested) => requested == NativeTarget ? HostTarget.Resolve() : requested;

    private static bool TryParseMode(string text, out BuildMode mode) =>
        Enum.TryParse(text, false, out mode) && Enum.IsDefined(mode);

    private static List<SourceInput> ReadSources(CompilerOptions options, DiagnosticBag diagnostics)
    {
        var sources = new List<SourceInput>();
        string root = Path.GetDirectoryName(Path.GetFullPath(options.Files[0]))!;
        foreach (string file in options.Files)
        {
            string full = Path.GetFullPath(file);
            if (!File.Exists(full))
            {
                diagnostics.Add(DiagnosticCode.CD0001, new Location(file, 0, 0), new Note(null, "file not found"));
                continue;
            }

            string display = Path.GetRelativePath(root, full).Replace('\\', '/');
            sources.Add(new SourceInput(full, display, File.ReadAllText(full)));
        }

        return sources;
    }
}
