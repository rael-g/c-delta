using CDelta.Ir;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FlowAnalysis;
using TypeKind = CDelta.Ir.TypeKind;

namespace CDelta.Frontend.CSharp;

internal sealed record FrameInfo(TypeId Type, Dictionary<ISymbol, int> Index);

internal sealed class ClosureFunctions(CSharpFrontend frontend)
{
    private readonly Dictionary<IMethodSymbol, FrameInfo> _outer = new(SymbolEqualityComparer.Default);

    public bool TryOuter(IMethodSymbol function, out FrameInfo frame) => _outer.TryGetValue(function, out frame!);

    public void SetOuter(IMethodSymbol function, FrameInfo frame) => _outer[function] = frame;

    private readonly Dictionary<IMethodSymbol, FunctionId> _lambdas = new(SymbolEqualityComparer.Default);
    private readonly Dictionary<IMethodSymbol, FunctionId> _thunks = new(SymbolEqualityComparer.Default);
    private readonly Dictionary<FunctionId, IMethodSymbol> _thunkTargets = [];

    public TypeId ContextType() =>
        frontend.Builder.Pointer(frontend.Builder.Primitive(TypeKind.Void), TypeFlags.Nullable | TypeFlags.Many);

    private readonly Dictionary<IMethodSymbol, string> _keys = new(SymbolEqualityComparer.Default);

    public string KeyOf(IMethodSymbol lambda) => _keys[lambda];

    public bool IsThunk(FunctionId function) => _thunkTargets.ContainsKey(function);

    private readonly Dictionary<IMethodSymbol, ControlFlowGraph> _graphs = new(SymbolEqualityComparer.Default);

    public bool TryGraph(IMethodSymbol lambda, out ControlFlowGraph graph) => _graphs.TryGetValue(lambda, out graph!);

    public FunctionId Lambda(IMethodSymbol lambda, string key, Location location, ControlFlowGraph graph, FrameInfo? frame = null)
    {
        _graphs.TryAdd(lambda, graph);
        if (frame is not null)
        {
            _outer[lambda] = frame;
        }

        if (_lambdas.TryGetValue(lambda, out FunctionId existing))
        {
            return existing;
        }

        FunctionId id = Declare(lambda, key, location, frame is null ? ContextType() : frontend.Builder.Pointer(frame.Type, TypeFlags.None));
        _keys.Add(lambda, key);
        _lambdas.Add(lambda, id);
        frontend.RegisterSymbol(lambda, id);
        return id;
    }

    public FunctionId Thunk(IMethodSymbol target, Location location)
    {
        target = target.OriginalDefinition;
        if (_thunks.TryGetValue(target, out FunctionId existing))
        {
            return existing;
        }

        FunctionId id = Declare(target, target.GetDocumentationCommentId()![2..] + "/thunk", location, ContextType());
        _thunks.Add(target, id);
        _thunkTargets.Add(id, target);
        frontend.ReserveSymbol();
        return id;
    }

    public Body ThunkBody(FunctionId thunk)
    {
        IMethodSymbol target = _thunkTargets[thunk];
        Module module = frontend.Module;
        var body = new Body();
        var builder = new FunctionBuilder(module, body);
        FunctionDecl declaration = module.Functions[thunk.Index];
        SourceSpan span = declaration.Span;
        var arguments = new List<OperandId>();
        for (int i = 0; i < declaration.Params.Count; i++)
        {
            ParamDecl parameter = module.Params[declaration.Params.Start + i];
            LocalId local = builder.NewLocal(module.Text(parameter.Name), parameter.Type, LocalKind.Param, span);
            if (i > 0)
            {
                arguments.Add(builder.Copy(builder.LocalPlace(local)));
            }
        }

        builder.SetBlock(builder.NewBlock());
        RvalueId call = builder.CallDirect(frontend.Declare(target), arguments);
        if (module.Types[declaration.Return.Index].Kind == TypeKind.Void)
        {
            builder.CallStatement(call, span);
            builder.Return(OperandId.None, span);
        }
        else
        {
            LocalId result = builder.NewLocal(null, declaration.Return, LocalKind.Temp, span);
            builder.Assign(builder.LocalPlace(result), call, span);
            builder.Return(builder.Copy(builder.LocalPlace(result)), span);
        }

        return body;
    }

    private FunctionId Declare(IMethodSymbol method, string key, Location location, TypeId context)
    {
        Module module = frontend.Module;
        var parameters = new List<ParamDecl> { new(module.Intern("ctx"), context, ParamMode.Value) };
        foreach (IParameterSymbol parameter in method.Parameters)
        {
            parameters.Add(new ParamDecl(module.Intern(parameter.Name), frontend.Types.ParameterType(parameter, location), TypeMapper.ModeOf(parameter.RefKind)));
        }

        return frontend.Builder.DeclareFunction(
            key,
            key,
            frontend.SpanOf(location),
            Origin.FromSource(module.Intern("csharp")),
            parameters,
            false,
            frontend.Types.ReferenceType(frontend.Types.Map(method.ReturnType, location), method.RefKind),
            CallConv.Default,
            LinkageKind.Internal,
            null,
            []);
    }
}
