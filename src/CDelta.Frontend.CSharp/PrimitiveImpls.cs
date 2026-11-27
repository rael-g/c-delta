using CDelta.Ir;
using Microsoft.CodeAnalysis;
using TypeKind = CDelta.Ir.TypeKind;

namespace CDelta.Frontend.CSharp;

internal sealed class PrimitiveImpls(CSharpFrontend frontend)
{
    private const int AdditiveIdentity = 0;
    private const int MultiplicativeIdentity = 1;

    private static readonly Dictionary<string, BinOp> _operators = new()
    {
        ["op_Addition"] = BinOp.Add,
        ["op_Subtraction"] = BinOp.Sub,
        ["op_Multiply"] = BinOp.Mul,
        ["op_Division"] = BinOp.Div,
        ["op_Equality"] = BinOp.Eq,
        ["op_Inequality"] = BinOp.Ne,
        ["op_LessThan"] = BinOp.Lt,
        ["op_LessThanOrEqual"] = BinOp.Le,
        ["op_GreaterThan"] = BinOp.Gt,
        ["op_GreaterThanOrEqual"] = BinOp.Ge,
    };

    private static readonly Dictionary<string, int> _constants = new()
    {
        ["get_Zero"] = AdditiveIdentity,
        ["get_AdditiveIdentity"] = AdditiveIdentity,
        ["get_One"] = MultiplicativeIdentity,
        ["get_MultiplicativeIdentity"] = MultiplicativeIdentity,
    };

    private readonly HashSet<SpecialType> _declared = [];
    private readonly Dictionary<FunctionId, Synthesis> _bodies = [];

    private sealed record Synthesis(string Member, TypeId Result, IReadOnlyList<(string Name, TypeId Type)> Parameters);

    public bool Owns(FunctionId function) => _bodies.ContainsKey(function);

    public void Ensure(ITypeSymbol type)
    {
        if (type is INamedTypeSymbol { IsGenericType: true } generic)
        {
            foreach (ITypeSymbol argument in generic.TypeArguments)
            {
                Ensure(argument);
            }
        }

        if (type is not INamedTypeSymbol { SpecialType: not SpecialType.None, TypeKind: Microsoft.CodeAnalysis.TypeKind.Struct } primitive
            || primitive.AllInterfaces.IsEmpty
            || !_declared.Add(primitive.SpecialType))
        {
            return;
        }

        Declare(primitive);
    }

    public Body Lower(FunctionId function)
    {
        Synthesis synthesis = _bodies[function];
        Module module = frontend.Module;
        var body = new Body();
        var builder = new FunctionBuilder(module, body);
        var arguments = new List<OperandId>();
        foreach ((string name, TypeId type) in synthesis.Parameters)
        {
            arguments.Add(builder.Copy(builder.LocalPlace(builder.NewLocal(name, type, LocalKind.Param, SourceSpan.None))));
        }

        builder.SetBlock(builder.NewBlock());
        RvalueId value = _operators.TryGetValue(synthesis.Member, out BinOp op)
            ? builder.Binop(op, ArithMode.Default, arguments[0], arguments[1])
            : builder.Use(builder.Const(Constant(synthesis.Result, _constants[synthesis.Member])));
        LocalId result = builder.NewLocal(null, synthesis.Result, LocalKind.Temp, SourceSpan.None);
        builder.Assign(builder.LocalPlace(result), value, SourceSpan.None);
        builder.Return(builder.Copy(builder.LocalPlace(result)), SourceSpan.None);
        return body;
    }

    private ConstId Constant(TypeId type, int value)
    {
        TypeKind kind = frontend.Module.Types[type.Index].Kind;
        return kind switch
        {
            TypeKind.F32 => frontend.Builder.FloatConstant(type, BitConverter.SingleToUInt32Bits(value)),
            TypeKind.F64 => frontend.Builder.FloatConstant(type, BitConverter.DoubleToUInt64Bits(value)),
            _ => frontend.Builder.IntConstant(type, value),
        };
    }

    private void Declare(INamedTypeSymbol primitive)
    {
        Module module = frontend.Module;
        TypeId self = frontend.Types.Map(primitive, Location.None);
        foreach (INamedTypeSymbol contract in primitive.AllInterfaces)
        {
            var methods = new List<ImplMethod>();
            foreach (IMethodSymbol member in contract.GetMembers().OfType<IMethodSymbol>())
            {
                if (_operators.ContainsKey(member.Name) || _constants.ContainsKey(member.Name))
                {
                    methods.Add(new ImplMethod(module.Intern(member.Name), Function(primitive, member), TypeListId.None));
                }
            }

            TypeId interfaceType = frontend.Types.Map(contract, Location.None);
            string key = primitive.Name + ":" + IrPrinter.TypeText(module, interfaceType);
            frontend.Builder.DeclareImpl(key, self, interfaceType, methods);
        }
    }

    private FunctionId Function(INamedTypeSymbol primitive, IMethodSymbol member)
    {
        Module module = frontend.Module;
        string name = primitive.Name + "." + member.Name;
        var parameters = new List<(string Name, TypeId Type)>();
        foreach (IParameterSymbol parameter in member.Parameters)
        {
            parameters.Add((parameter.Name, frontend.Types.Map(parameter.Type, Location.None)));
        }

        TypeId result = frontend.Types.Map(member.ReturnType, Location.None);
        FunctionId id = frontend.Builder.DeclareFunction(
            name,
            name,
            SourceSpan.None,
            Origin.Synthetic,
            [.. parameters.Select(p => new ParamDecl(module.Intern(p.Name), p.Type, ParamMode.Value))],
            false,
            result,
            CallConv.Default,
            LinkageKind.Internal,
            null,
            [],
            ParamMode.Value);
        frontend.ReserveSymbol();
        _bodies.Add(id, new Synthesis(member.Name, result, parameters));
        return id;
    }
}
