using DiagnosticBag = CDelta.Diagnostics.DiagnosticBag;
using DiagnosticCode = CDelta.Diagnostics.DiagnosticCode;
using Note = CDelta.Diagnostics.Note;
using CDelta.Ir;
using TypeKind = CDelta.Ir.TypeKind;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace CDelta.Frontend.CSharp;

public sealed class CSharpFrontend : IBodyProvider
{
    private const string Language = "csharp";
    private const string EntryPointAttribute = "cdelta.EntryPoint";
    private const string MainName = "Main";

    private static readonly SymbolDisplayFormat _typeFormat = new(
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces);

    private readonly CSharpCompilation _compilation;
    private readonly DiagnosticBag _diagnostics;
    private readonly Dictionary<IMethodSymbol, FunctionId> _functions = new(SymbolEqualityComparer.Default);
    private readonly List<IMethodSymbol> _symbols = [];
    private readonly Dictionary<string, SourceId> _sourceIds = [];
    private readonly TypeMapper _types;

    private CSharpFrontend(CSharpCompilation compilation, ModuleBuilder builder, DiagnosticBag diagnostics)
    {
        _compilation = compilation;
        Builder = builder;
        _diagnostics = diagnostics;
        _types = new TypeMapper(builder);
    }

    public ModuleBuilder Builder { get; }

    public Module Module => Builder.Module;

    public IReadOnlyList<string> BindingErrors { get; private init; } = [];

    public static CSharpFrontend Create(
        IReadOnlyList<SourceInput> sources,
        string corelibPath,
        string packageName,
        DiagnosticBag diagnostics)
    {
        CSharpCompilation compilation = CompilationFactory.Create(sources, corelibPath, packageName);
        var builder = new ModuleBuilder(new Module(packageName));
        var frontend = new CSharpFrontend(compilation, builder, diagnostics)
        {
            BindingErrors = compilation.GetDiagnostics()
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .Select(FormatBindingError)
                .ToList(),
        };
        foreach (SourceInput source in sources)
        {
            frontend._sourceIds[source.FullPath] = builder.AddSource(source.FullPath, source.DisplayPath);
        }

        return frontend;
    }

    private static string FormatBindingError(Microsoft.CodeAnalysis.Diagnostic diagnostic)
    {
        FileLinePositionSpan span = diagnostic.Location.GetLineSpan();
        return span.Path + "(" + (span.StartLinePosition.Line + 1) + "," + (span.StartLinePosition.Character + 1) + "): error "
            + diagnostic.Id + ": " + diagnostic.GetMessage(System.Globalization.CultureInfo.InvariantCulture);
    }

    public IReadOnlyList<FunctionId> DeclareRoots()
    {
        var roots = new List<FunctionId>();
        foreach (IMethodSymbol method in EntryPointCandidates())
        {
            roots.Add(Declare(method, [EntryPointAttribute]));
        }

        return roots;
    }

    public FunctionId Declare(IMethodSymbol method) => Declare(method, []);

    public Body? Lower(FunctionId function)
    {
        IMethodSymbol method = _symbols[function.Index];
        if (method.IsExtern)
        {
            return null;
        }

        try
        {
            return new BodyLowerer(this, method).Lower();
        }
        catch (UnsupportedConstructException exception)
        {
            Report(DiagnosticCode.CD9001, exception.SourceLocation, exception.Message);
            return null;
        }
    }

    internal CSharpCompilation Compilation => _compilation;

    internal TypeMapper Types => _types;

    internal SourceSpan SpanOf(Location location)
    {
        if (location.SourceTree is null || !_sourceIds.TryGetValue(location.SourceTree.FilePath, out SourceId file))
        {
            return SourceSpan.None;
        }

        LinePosition start = location.GetLineSpan().StartLinePosition;
        return new SourceSpan(file, start.Line + 1, start.Character + 1);
    }

    internal void Report(DiagnosticCode code, Location location, string note)
    {
        FileLinePositionSpan span = location.GetLineSpan();
        var at = new CDelta.Diagnostics.Location(span.Path, span.StartLinePosition.Line + 1, span.StartLinePosition.Character + 1);
        _diagnostics.Add(code, at, new Note(null, note));
    }

    private IEnumerable<IMethodSymbol> EntryPointCandidates()
    {
        var pending = new Stack<INamespaceOrTypeSymbol>();
        pending.Push(_compilation.Assembly.GlobalNamespace);
        var found = new List<IMethodSymbol>();
        while (pending.Count > 0)
        {
            foreach (ISymbol member in pending.Pop().GetMembers())
            {
                switch (member)
                {
                    case INamespaceOrTypeSymbol container:
                        pending.Push(container);
                        break;
                    case IMethodSymbol { Name: MainName, IsStatic: true, Parameters.Length: 0 } method
                        when method.ReturnType.SpecialType is SpecialType.System_Void or SpecialType.System_Int32
                            && method.TypeParameters.Length == 0:
                        found.Add(method);
                        break;
                }
            }
        }

        return found.OrderBy(m => m.GetDocumentationCommentId(), StringComparer.Ordinal);
    }

    private FunctionId Declare(IMethodSymbol method, IReadOnlyList<string> attributes)
    {
        method = method.OriginalDefinition;
        if (_functions.TryGetValue(method, out FunctionId existing))
        {
            return existing;
        }

        Location location = method.Locations.FirstOrDefault() ?? Location.None;
        string key = method.GetDocumentationCommentId()![2..];
        string name = method.ContainingType.ToDisplayString(_typeFormat) + "." + method.Name;
        Module module = Module;

        var parameters = new List<ParamDecl>();
        foreach (IParameterSymbol parameter in method.Parameters)
        {
            if (parameter.RefKind != RefKind.None)
            {
                throw new UnsupportedConstructException("reference parameters are not supported yet", location);
            }

            parameters.Add(new ParamDecl(module.Intern(parameter.Name), _types.Map(parameter.Type, location), ParamMode.Value));
        }

        var origin = Origin.FromSource(module.Intern(Language));
        LinkageKind linkage = LinkageKind.Internal;
        CallConv conv = CallConv.Default;
        if (method.IsExtern)
        {
            DllImportData? import = method.GetDllImportData();
            if (import is null)
            {
                throw new UnsupportedConstructException("an extern method needs [DllImport] at this stage", location);
            }

            origin = Origin.FromLibrary(
                module.Intern(import.ModuleName ?? string.Empty),
                module.Intern(import.EntryPointName ?? method.Name));
            linkage = LinkageKind.Extern;
            conv = import.CallingConvention switch
            {
                System.Runtime.InteropServices.CallingConvention.Cdecl => CallConv.Cdecl,
                System.Runtime.InteropServices.CallingConvention.StdCall => CallConv.Stdcall,
                System.Runtime.InteropServices.CallingConvention.FastCall => CallConv.Fastcall,
                System.Runtime.InteropServices.CallingConvention.ThisCall => CallConv.Thiscall,
                _ => CallConv.Default,
            };
        }

        FunctionId id = Builder.DeclareFunction(
            key,
            name,
            SpanOf(location),
            origin,
            parameters,
            false,
            _types.Map(method.ReturnType, location),
            conv,
            linkage,
            null,
            attributes);
        _functions.Add(method, id);
        _symbols.Add(method);
        return id;
    }
}
