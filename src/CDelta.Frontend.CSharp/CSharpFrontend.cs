using DiagnosticBag = CDelta.Diagnostics.DiagnosticBag;
using DiagnosticCode = CDelta.Diagnostics.DiagnosticCode;
using Note = CDelta.Diagnostics.Note;
using CDelta.Ir;
using TypeKind = CDelta.Ir.TypeKind;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace CDelta.Frontend.CSharp;

public sealed class CSharpFrontend : IBodyProvider
{
    private const string Language = "csharp";
    private const string EntryPointAttribute = "cdelta.EntryPoint";
    private const string PanicHandlerAttribute = "cdelta.PanicHandler";
    private const string PanicHandlerType = "CDelta.PanicHandlerAttribute";
    private const string MainName = "Main";

    private static readonly SymbolDisplayFormat _typeFormat = new(
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces);

    private readonly CSharpCompilation _compilation;
    private readonly DiagnosticBag _diagnostics;
    private readonly Dictionary<IMethodSymbol, FunctionId> _functions = new(SymbolEqualityComparer.Default);
    private readonly List<IMethodSymbol> _symbols = [];
    private readonly Dictionary<IFieldSymbol, GlobalId> _globals = new(SymbolEqualityComparer.Default);
    private readonly Dictionary<string, SourceId> _sourceIds = [];
    private readonly TypeMapper _types;

    private CSharpFrontend(CSharpCompilation compilation, ModuleBuilder builder, DiagnosticBag diagnostics)
    {
        _compilation = compilation;
        Builder = builder;
        _diagnostics = diagnostics;
        Primitives = new PrimitiveImpls(this);
        Closures = new ClosureFunctions(this);
        _types = new TypeMapper(builder, location => SpanOf(location)) { ImportIdOf = ImportIdOf, StructDefined = QueueImpls, ImplementersOf = ImplementersOf };
    }

    public ModuleBuilder Builder { get; }

    public Module Module => Builder.Module;

    public IReadOnlyList<string> BindingErrors { get; private init; } = [];

    public static CSharpFrontend Create(
        IReadOnlyList<SourceInput> sources,
        string corelibPath,
        string packageName,
        IReadOnlyList<string> preprocessorSymbols,
        ImmutableArray<DiagnosticAnalyzer> analyzers,
        DiagnosticBag diagnostics)
    {
        CSharpCompilation compilation = CompilationFactory.Create(sources, corelibPath, packageName, preprocessorSymbols);
        var builder = new ModuleBuilder(new Module(packageName));
        var frontend = new CSharpFrontend(compilation, builder, diagnostics)
        {
            BindingErrors = ErrorsOf(compilation, analyzers),
        };
        foreach (SourceInput source in sources)
        {
            frontend._sourceIds[source.FullPath] = builder.AddSource(source.FullPath, source.DisplayPath);
        }

        return frontend;
    }

    private static List<string> ErrorsOf(CSharpCompilation compilation, ImmutableArray<DiagnosticAnalyzer> analyzers)
    {
        ImmutableArray<Microsoft.CodeAnalysis.Diagnostic> all = analyzers.IsDefaultOrEmpty
            ? compilation.GetDiagnostics()
            : compilation.WithAnalyzers(analyzers).GetAllDiagnosticsAsync().GetAwaiter().GetResult();
        return all
            .Where(d => d.Severity == DiagnosticSeverity.Error && !d.IsSuppressed)
            .OrderBy(d => d.Location.GetLineSpan().Path, StringComparer.Ordinal)
            .ThenBy(d => d.Location.SourceSpan.Start)
            .ThenBy(d => d.Id, StringComparer.Ordinal)
            .Select(FormatBindingError)
            .ToList();
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

        foreach (IMethodSymbol method in PanicHandlerCandidates())
        {
            roots.Add(Declare(method, [PanicHandlerAttribute]));
        }

        return roots;
    }

    public FunctionId Declare(IMethodSymbol method) => Declare(method, []);

    internal PrimitiveImpls Primitives { get; }

    internal ClosureFunctions Closures { get; }

    internal string KeyOf(IMethodSymbol method) =>
        method.MethodKind == MethodKind.AnonymousFunction ? Closures.KeyOf(method)
        : IsTopLevelEntry(method) ? TopLevelEntryName : method.GetDocumentationCommentId()![2..];

    internal void RegisterSymbol(IMethodSymbol symbol, FunctionId id)
    {
        _functions.Add(symbol, id);
        _symbols.Add(symbol);
    }

    internal void ReserveSymbol() => _symbols.Add(null!);

    public Body? Lower(FunctionId function)
    {
        if (Primitives.Owns(function))
        {
            return Primitives.Lower(function);
        }

        if (Closures.IsThunk(function))
        {
            return Closures.ThunkBody(function);
        }

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

    internal bool IsReadOnlyMember(IMethodSymbol method) => method.IsReadOnly || method.ContainingType.IsReadOnly;

    internal GlobalId DeclareGlobal(IFieldSymbol field)
    {
        field = field.OriginalDefinition;
        if (_globals.TryGetValue(field, out GlobalId existing))
        {
            return existing;
        }

        Location location = field.Locations.FirstOrDefault() ?? Location.None;
        Module module = Module;
        TypeId type;
        ConstId initializer;
        if (field.Type is IArrayTypeSymbol array)
        {
            (type, initializer) = ArrayGlobal(field, array, location);
        }
        else
        {
            type = _types.Map(field.Type, location);
            initializer = StaticInitializer(field, type, location);
        }

        GlobalId id = Builder.DeclareGlobal(
            field.GetDocumentationCommentId()![2..],
            field.ContainingType.ToDisplayString(_typeFormat) + "." + field.Name,
            SpanOf(location),
            Origin.FromSource(module.Intern(Language)),
            type,
            !field.IsReadOnly,
            field.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() == "System.ThreadStaticAttribute"),
            initializer,
            LinkageKind.Internal,
            null);
        _globals.Add(field, id);
        return id;
    }

    private (TypeId Type, ConstId Initializer) ArrayGlobal(IFieldSymbol field, IArrayTypeSymbol array, Location location)
    {
        TypeId element = _types.Map(array.ElementType, location);
        foreach (SyntaxReference reference in field.DeclaringSyntaxReferences)
        {
            if (reference.GetSyntax() is not Microsoft.CodeAnalysis.CSharp.Syntax.VariableDeclaratorSyntax { Initializer: { } initializer })
            {
                continue;
            }

            SemanticModel model = _compilation.GetSemanticModel(initializer.SyntaxTree);
            IEnumerable<Microsoft.CodeAnalysis.CSharp.Syntax.ExpressionSyntax>? items = initializer.Value switch
            {
                Microsoft.CodeAnalysis.CSharp.Syntax.CollectionExpressionSyntax collection =>
                    collection.Elements.OfType<Microsoft.CodeAnalysis.CSharp.Syntax.ExpressionElementSyntax>().Select(e => e.Expression),
                Microsoft.CodeAnalysis.CSharp.Syntax.ArrayCreationExpressionSyntax { Initializer: { } list } => list.Expressions,
                Microsoft.CodeAnalysis.CSharp.Syntax.ImplicitArrayCreationExpressionSyntax implicitArray => implicitArray.Initializer.Expressions,
                _ => null,
            };
            if (items is null)
            {
                Report(DiagnosticCode.CD4006, initializer.GetLocation(), "this initializer is not a list of constants");
                break;
            }

            var constants = new List<ConstId>();
            foreach (Microsoft.CodeAnalysis.CSharp.Syntax.ExpressionSyntax item in items)
            {
                Optional<object?> value = model.GetConstantValue(item);
                if (!value.HasValue)
                {
                    Report(DiagnosticCode.CD4006, item.GetLocation(), "this element is not a constant");
                    constants.Add(ZeroConstant(element));
                }
                else
                {
                    constants.Add(ConstantOf(value.Value, element, array.ElementType));
                }
            }

            TypeId listed = Builder.ArrayOf(element, constants.Count);
            return (listed, Builder.AggregateConstant(listed, constants));
        }

        TypeId empty = Builder.ArrayOf(element, 0);
        return (empty, Builder.AggregateConstant(empty, []));
    }

    private ConstId StaticInitializer(IFieldSymbol field, TypeId type, Location location)
    {
        foreach (SyntaxReference reference in field.DeclaringSyntaxReferences)
        {
            if (reference.GetSyntax() is Microsoft.CodeAnalysis.CSharp.Syntax.VariableDeclaratorSyntax { Initializer: { } initializer })
            {
                SemanticModel model = _compilation.GetSemanticModel(initializer.SyntaxTree);
                Optional<object?> constant = model.GetConstantValue(initializer.Value);
                if (constant.HasValue)
                {
                    return ConstantOf(constant.Value, type, field.Type);
                }

                Report(DiagnosticCode.CD4006, initializer.GetLocation(), "this initializer is not a constant");
                return ZeroConstant(type);
            }
        }

        return ZeroConstant(type);
    }

    private ConstId ConstantOf(object? value, TypeId type, ITypeSymbol symbol)
    {
        if (value is null)
        {
            return Builder.NullConstant(type);
        }

        if (value is bool flag)
        {
            return Builder.BoolConstant(type, flag);
        }

        Int128 number = value switch
        {
            sbyte v => v,
            byte v => v,
            short v => v,
            ushort v => v,
            int v => v,
            uint v => v,
            long v => v,
            ulong v => v,
            char v => v,
            _ => throw new UnsupportedConstructException("the constant of type " + symbol.ToDisplayString() + " is not supported yet", Location.None),
        };
        return Builder.IntConstant(type, number);
    }

    private ConstId ZeroConstant(TypeId type)
    {
        TypeNode node = Module.Types[type.Index];
        switch (node.Kind)
        {
            case TypeKind.Bool:
                return Builder.BoolConstant(type, false);
            case TypeKind.Ptr:
                return Builder.NullConstant(type);
            case TypeKind.F32 or TypeKind.F64 or TypeKind.F16:
                return Builder.FloatConstant(type, 0);
            case TypeKind.Array:
                var elements = new List<ConstId>();
                for (int i = 0; i < node.B; i++)
                {
                    elements.Add(ZeroConstant(new TypeId(node.A)));
                }

                return Builder.AggregateConstant(type, elements);
            case TypeKind.Struct:
                StructDecl declaration = Module.Structs[node.A];
                var fields = new List<ConstId>();
                for (int i = 0; i < declaration.Fields.Count; i++)
                {
                    fields.Add(ZeroConstant(Module.Fields[declaration.Fields.Start + i].Type));
                }

                return Builder.AggregateConstant(type, fields);
            default:
                return Builder.IntConstant(type, 0);
        }
    }

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

    private static bool IsMainReturn(ITypeSymbol type) =>
        type.SpecialType is SpecialType.System_Void or SpecialType.System_Int32
        || type is INamedTypeSymbol { Name: "Result", ContainingNamespace: { Name: "CDelta", ContainingNamespace.IsGlobalNamespace: true } } result
            && (result.TypeArguments.Length == 0 || result.TypeArguments.Length == 1 && result.TypeArguments[0].SpecialType == SpecialType.System_Int32);

    private IEnumerable<IMethodSymbol> PanicHandlerCandidates() =>
        MethodsOfAssembly()
            .Where(m => m.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() == PanicHandlerType))
            .OrderBy(m => m.GetDocumentationCommentId(), StringComparer.Ordinal)
            .Take(1);

    private IEnumerable<IMethodSymbol> MethodsOfAssembly()
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
                    case IMethodSymbol method:
                        found.Add(method);
                        break;
                }
            }
        }

        return found;
    }

    private const string TopLevelEntryName = "Program.Main";

    internal static bool IsTopLevelEntry(IMethodSymbol method) =>
        method.Name == "<Main>$";

    private IEnumerable<IMethodSymbol> EntryPointCandidates()
    {
        if (_compilation.GetEntryPoint(default) is { } entry && IsTopLevelEntry(entry))
        {
            return [entry];
        }

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
                        when IsMainReturn(method.ReturnType) && method.TypeParameters.Length == 0:
                        found.Add(method);
                        break;
                }
            }
        }

        return found.OrderBy(m => m.GetDocumentationCommentId(), StringComparer.Ordinal);
    }

    private IReadOnlyList<INamedTypeSymbol> ImplementersOf(INamedTypeSymbol contract)
    {
        var found = new List<INamedTypeSymbol>();
        void Visit(INamespaceOrTypeSymbol container)
        {
            foreach (INamedTypeSymbol type in container.GetTypeMembers())
            {
                if (type.TypeKind == Microsoft.CodeAnalysis.TypeKind.Struct && type.AllInterfaces.Any(candidate => SymbolEqualityComparer.Default.Equals(candidate.OriginalDefinition, contract)))
                {
                    found.Add(type);
                }

                Visit(type);
            }

            if (container is INamespaceSymbol space)
            {
                foreach (INamespaceSymbol nested in space.GetNamespaceMembers())
                {
                    Visit(nested);
                }
            }
        }

        Visit(_compilation.Assembly.GlobalNamespace);
        return found
            .OrderBy(type => type.Locations[0].SourceTree?.FilePath, StringComparer.Ordinal)
            .ThenBy(type => type.Locations[0].SourceSpan.Start)
            .ToList();
    }

    private readonly Queue<(INamedTypeSymbol Type, StructId Id)> _pendingImpls = new();
    private int _declaring;

    private void QueueImpls(INamedTypeSymbol type, StructId id)
    {
        _pendingImpls.Enqueue((type, id));
        if (_declaring == 0)
        {
            DrainImpls();
        }
    }

    private void DrainImpls()
    {
        while (_pendingImpls.Count > 0)
        {
            (INamedTypeSymbol type, StructId id) = _pendingImpls.Dequeue();
            DeclareImpls(type, id);
        }
    }

    private void DeclareImpls(INamedTypeSymbol type, StructId id)
    {
        Location location = type.Locations.FirstOrDefault() ?? Location.None;
        TypeId concrete = _types.Map(type, location);
        TypeListId typeArgs = type.IsGenericType
            ? Module.TypeList([.. type.TypeArguments.Select(argument => _types.Map(argument, location).Index)])
            : TypeListId.None;
        foreach (INamedTypeSymbol contract in type.AllInterfaces)
        {
            var methods = new List<ImplMethod>();
            foreach (IMethodSymbol member in contract.GetMembers().OfType<IMethodSymbol>())
            {
                if (member.MethodKind is not (MethodKind.Ordinary or MethodKind.PropertyGet or MethodKind.UserDefinedOperator)
                    || type.FindImplementationForInterfaceMember(member) is not IMethodSymbol implementation)
                {
                    continue;
                }

                methods.Add(new ImplMethod(Module.Intern(member.Name), Declare(implementation), typeArgs));
            }

            TypeId interfaceType = _types.InterfaceType(contract, location);
            string key = type.GetDocumentationCommentId()![2..] + ":" + IrPrinter.TypeText(Module, interfaceType);
            Builder.DeclareImpl(key, concrete, interfaceType, methods);
        }
    }

    private FunctionId Declare(IMethodSymbol method, IReadOnlyList<string> attributes)
    {
        _declaring++;
        try
        {
            return DeclareFunction(method, attributes);
        }
        finally
        {
            _declaring--;
            if (_declaring == 0)
            {
                DrainImpls();
            }
        }
    }

    private FunctionId DeclareFunction(IMethodSymbol method, IReadOnlyList<string> attributes)
    {
        method = method.OriginalDefinition;
        if (_functions.TryGetValue(method, out FunctionId existing))
        {
            return existing;
        }

        Location location = method.Locations.FirstOrDefault() ?? Location.None;
        bool topLevel = IsTopLevelEntry(method);
        string key = topLevel ? TopLevelEntryName : method.GetDocumentationCommentId()![2..];
        string name = topLevel ? TopLevelEntryName : method.ContainingType.ToDisplayString(_typeFormat) + "." + method.Name;
        Module module = Module;

        var parameters = new List<ParamDecl>();
        if (!method.IsStatic)
        {
            bool readOnly = IsReadOnlyMember(method);
            TypeId self = _types.Map(method.ContainingType, location);
            parameters.Add(new ParamDecl(
                module.Intern("this"),
                Builder.Pointer(self, readOnly ? TypeFlags.Const : TypeFlags.None),
                readOnly ? ParamMode.RefReadonly : ParamMode.Ref));
        }

        foreach (IParameterSymbol parameter in topLevel ? [] : method.Parameters)
        {
            parameters.Add(new ParamDecl(
                module.Intern(parameter.Name),
                _types.ParameterType(parameter, location),
                TypeMapper.ModeOf(parameter.RefKind)));
        }

        var origin = Origin.FromSource(module.Intern(Language));
        LinkageKind linkage = LinkageKind.Internal;
        CallConv conv = CallConv.Default;
        if (method.IsExtern)
        {
            DllImportData? import = method.GetDllImportData();
            int importId = ImportIdOf(method.ContainingType);
            if (import is null && importId < 0)
            {
                throw new UnsupportedConstructException("an extern method needs [DllImport] at this stage", location);
            }

            linkage = LinkageKind.Extern;
            if (import is null)
            {
                origin = new Origin(OriginKind.CHeader, StringId.None, importId, StringId.None, module.Intern(CNameOf(method)));
            }
            else
            {
                origin = Origin.FromLibrary(
                    module.Intern(import.ModuleName ?? string.Empty),
                    module.Intern(import.EntryPointName ?? method.Name));
                conv = import.CallingConvention switch
                {
                    System.Runtime.InteropServices.CallingConvention.Cdecl => CallConv.Cdecl,
                    System.Runtime.InteropServices.CallingConvention.StdCall => CallConv.Stdcall,
                    System.Runtime.InteropServices.CallingConvention.FastCall => CallConv.Fastcall,
                    System.Runtime.InteropServices.CallingConvention.ThisCall => CallConv.Thiscall,
                    _ => CallConv.Default,
                };
            }
        }

        FunctionId id = Builder.DeclareFunction(
            key,
            name,
            SpanOf(location),
            origin,
            parameters,
            method.IsVararg,
            _types.ReferenceType(_types.Map(method.ReturnType, location), method.RefKind),
            conv,
            linkage,
            null,
            attributes,
            TypeMapper.ModeOf(method.RefKind));
        var typeParameters = new List<string>();
        if (method.ContainingType.IsGenericType)
        {
            typeParameters.AddRange(method.ContainingType.TypeParameters.Select(parameter => parameter.Name));
        }

        typeParameters.AddRange(method.TypeParameters.Select(parameter => parameter.Name));
        Builder.SetTypeParams(id, typeParameters);
        _functions.Add(method, id);
        _symbols.Add(method);
        return id;
    }

    private readonly Dictionary<INamedTypeSymbol, int> _imports = new(SymbolEqualityComparer.Default);

    internal int ImportIdOf(INamedTypeSymbol? type)
    {
        for (INamedTypeSymbol? current = type; current is not null; current = current.ContainingType)
        {
            AttributeData? attribute = current.GetAttributes().FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == "CDelta.ImportAttribute");
            if (attribute is null)
            {
                continue;
            }

            if (_imports.TryGetValue(current, out int existing))
            {
                return existing;
            }

            var headers = new List<string>();
            if (attribute.ConstructorArguments.Length > 0)
            {
                headers.AddRange(attribute.ConstructorArguments[0].Values.Select(v => (string)v.Value!));
            }

            List<string> Named(string name) =>
                [.. attribute.NamedArguments.Where(a => a.Key == name).SelectMany(a => a.Value.Values).Select(v => (string)v.Value!)];
            int id = Builder.AddCImport(new CImportDecl(headers, Named("Defines"), Named("IncludeDirs")));
            _imports[current] = id;
            return id;
        }

        return -1;
    }

    internal static string CNameOf(ISymbol symbol)
    {
        AttributeData? attribute = symbol.GetAttributes().FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == "CDelta.CNameAttribute");
        return attribute is { ConstructorArguments.Length: > 0 } ? (string)attribute.ConstructorArguments[0].Value! : symbol.Name;
    }
}
