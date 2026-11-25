using CDelta.Ir;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using TypeKind = CDelta.Ir.TypeKind;

namespace CDelta.Frontend.CSharp;

internal sealed partial class BodyLowerer
{
    private const string ErrorNameIntrinsic = "error.name";

    private static bool IsCDeltaType(ITypeSymbol? type, string name) =>
        type is INamedTypeSymbol { ContainingNamespace: { Name: "CDelta", ContainingNamespace.IsGlobalNamespace: true } } named && named.Name == name;

    private static bool IsResultType(ITypeSymbol? type) => IsCDeltaType(type, "Result");

    private static bool IsErrorType(ITypeSymbol? type) => IsCDeltaType(type, "Error");

    private static bool IsIntrinsicCall(IInvocationOperation invocation, string name) =>
        invocation.TargetMethod.Name == name && invocation.TargetMethod.ContainingType.ToDisplayString() == "CDelta.Intrinsics";

    private static bool IsErrorConversion(IOperation? value)
    {
        while (value is IParenthesizedOperation parenthesized)
        {
            value = parenthesized.Operand;
        }

        return value is IConversionOperation { Operand.Type: { } source } conversion
            && IsResultType(conversion.Type)
            && (IsErrorType(source) || source.TypeKind == Microsoft.CodeAnalysis.TypeKind.Enum);
    }

    private bool TryLowerErrorOperation(IOperation operation, out OperandId? value)
    {
        value = null;
        switch (operation)
        {
            case IInvocationOperation invocation when IsIntrinsicCall(invocation, "Try"):
                value = LowerTry(invocation);
                return true;
            case IInvocationOperation invocation when IsIntrinsicCall(invocation, "Catch"):
                value = LowerCatch(invocation);
                return true;
            case IInvocationOperation invocation when IsIntrinsicCall(invocation, "Fail"):
                value = LowerFail(invocation);
                return true;
            case IInvocationOperation { TargetMethod.Name: "TryGet" } tryGet when IsResultType(tryGet.TargetMethod.ContainingType):
                value = LowerTryGet(tryGet);
                return true;
            case IPropertyReferenceOperation { Instance: { } instance } property when IsResultType(instance.Type):
                value = LowerResultMember(property, instance);
                return true;
            case IPropertyReferenceOperation { Instance: { } errorInstance, Property.Name: "Name" } when IsErrorType(errorInstance.Type):
                value = LowerErrorName(errorInstance);
                return true;
            case IConversionOperation { Operand.Type: not null } conversion when IsResultType(conversion.Type) && !IsResultType(conversion.Operand.Type):
                value = LowerToResult(conversion);
                return true;
            case IDefaultValueOperation when IsResultType(operation.Type):
                value = LowerDefaultResult(operation);
                return true;
            default:
                return false;
        }
    }

    private TypeId BoolType() => _builder.Primitive(TypeKind.Bool);

    private TypeId PayloadTypeOf(TypeId result) => new(_module.Types[result.Index].A);

    private TypeId ErrorTypeOf(TypeId result) => new(_module.Types[result.Index].B);

    private bool IsVoid(TypeId type) => _module.Types[type.Index].Kind == TypeKind.Void;

    private OperandId? ReadPayload(PlaceId result)
    {
        TypeId payload = PayloadTypeOf(IrTyper.OfPlace(_module, _body, result));
        return IsVoid(payload) ? null : _f.Copy(_f.Project(result, ProjectionKind.Payload, 0));
    }

    private OperandId ConvertError(OperandId error, TypeId target, SourceSpan span)
    {
        TypeId source = OperandType(error);
        return source == target ? error : Emit(target, _f.Cast(CastKind.ErrorError, ArithMode.Default, error, target), span);
    }

    private OperandId LowerTry(IInvocationOperation invocation)
    {
        SourceSpan span = SpanOf(invocation);
        PlaceId place = SpillToPlace(LowerValue(invocation.Arguments[0].Value), span);
        OperandId succeeded = Emit(BoolType(), _f.HasPayload(place), span);
        BlockId success = _f.NewBlock();
        BlockId failure = _f.NewBlock();
        _f.Branch(succeeded, success, failure, span);
        _f.SetBlock(failure);
        TypeId returnType = frontend.Types.Map(method.ReturnType, method.Locations[0]);
        OperandId error = Emit(ErrorTypeOf(IrTyper.OfPlace(_module, _body, place)), _f.Err(place), span);
        error = ConvertError(error, ErrorTypeOf(returnType), span);
        RunDefersAt(invocation.Syntax, error, span);
        _f.TracePush(span);
        OperandId failed = Emit(returnType, _f.ResultErr(returnType, error), span);
        _f.Return(failed, span);
        _f.SetBlock(success);
        return ReadPayload(place) ?? Zero(_builder.Primitive(TypeKind.Void), span);
    }

    private OperandId LowerCatch(IInvocationOperation invocation)
    {
        SourceSpan span = SpanOf(invocation);
        TypeId elementType = frontend.Types.Map(invocation.Type!, invocation.Syntax.GetLocation());
        LocalId saved = NewTemp(_builder.Primitive(TypeKind.U32));
        _f.TraceSave(saved, span);
        PlaceId place = SpillToPlace(LowerValue(invocation.Arguments[0].Value), span);
        OperandId fallback = Snapshot(LowerValue(invocation.Arguments[1].Value), span);
        LocalId result = NewTemp(elementType);
        OperandId succeeded = Emit(BoolType(), _f.HasPayload(place), span);
        BlockId success = _f.NewBlock();
        BlockId failure = _f.NewBlock();
        BlockId join = _f.NewBlock();
        _f.Branch(succeeded, success, failure, span);
        _f.SetBlock(success);
        _f.Assign(_f.LocalPlace(result), _f.Use(ReadPayload(place)!.Value), span);
        _f.Goto(join, span);
        _f.SetBlock(failure);
        _f.TraceRestore(saved, span);
        _f.Assign(_f.LocalPlace(result), _f.Use(fallback), span);
        _f.Goto(join, span);
        _f.SetBlock(join);
        return _f.Copy(_f.LocalPlace(result));
    }

    private OperandId LowerFail(IInvocationOperation invocation)
    {
        SourceSpan span = SpanOf(invocation);
        IOperation argument = invocation.Arguments[0].Value;
        OperandId error = ErrorOfEnum(argument, span);
        return ConvertError(error, _builder.AnyError(), span);
    }

    private OperandId ErrorOfEnum(IOperation enumValue, SourceSpan span)
    {
        var enumType = (INamedTypeSymbol)enumValue.Type!;
        TypeId set = frontend.Types.ErrorSetOf(enumType);
        if (enumValue.ConstantValue is { HasValue: true, Value: { } constant })
        {
            IFieldSymbol member = enumType.GetMembers().OfType<IFieldSymbol>().First(f => f.HasConstantValue && Equals(f.ConstantValue, constant));
            return _f.Const(_builder.ErrorConstant(set, member.Name));
        }

        OperandId value = LowerValue(enumValue);
        return Emit(set, _f.Cast(CastKind.EnumError, ArithMode.Default, value, set), span);
    }

    private OperandId LowerToResult(IConversionOperation conversion)
    {
        SourceSpan span = SpanOf(conversion);
        TypeId resultType = frontend.Types.Map(conversion.Type!, conversion.Syntax.GetLocation());
        ITypeSymbol source = conversion.Operand.Type!;
        if (IsErrorType(source))
        {
            return Emit(resultType, _f.ResultErr(resultType, LowerValue(conversion.Operand)), span);
        }

        if (source.TypeKind == Microsoft.CodeAnalysis.TypeKind.Enum && source.Equals(((INamedTypeSymbol)conversion.Type!).TypeArguments.ElementAtOrDefault(1), SymbolEqualityComparer.Default))
        {
            OperandId error = ErrorOfEnum(conversion.Operand, span);
            return Emit(resultType, _f.ResultErr(resultType, error), span);
        }

        return Emit(resultType, _f.ResultOk(resultType, LowerValue(conversion.Operand)), span);
    }

    private OperandId LowerDefaultResult(IOperation operation)
    {
        SourceSpan span = SpanOf(operation);
        TypeId resultType = frontend.Types.Map(operation.Type!, operation.Syntax.GetLocation());
        TypeId payload = PayloadTypeOf(resultType);
        return Emit(resultType, _f.ResultOk(resultType, IsVoid(payload) ? OperandId.None : Zero(payload, span)), span);
    }

    private OperandId LowerResultMember(IPropertyReferenceOperation property, IOperation instance)
    {
        SourceSpan span = SpanOf(property);
        PlaceId place = PlaceOf(instance);
        switch (property.Property.Name)
        {
            case "IsError":
                OperandId succeeded = Emit(BoolType(), _f.HasPayload(place), span);
                return Emit(BoolType(), _f.Unop(UnOp.Not, ArithMode.Default, succeeded), span);
            case "Value":
                return ReadPayload(place) ?? throw new UnsupportedConstructException("Value of a result without a payload", property.Syntax.GetLocation());
            case "Error":
                return ReadError(place, instance.Type!, span);
            default:
                throw new UnsupportedConstructException("the member " + property.Property.Name + " of a result is not supported", property.Syntax.GetLocation());
        }
    }

    private OperandId ReadError(PlaceId place, ITypeSymbol resultSymbol, SourceSpan span)
    {
        TypeId errorType = ErrorTypeOf(IrTyper.OfPlace(_module, _body, place));
        OperandId error = Emit(errorType, _f.Err(place), span);
        var result = (INamedTypeSymbol)resultSymbol;
        if (result.TypeArguments.Length != 2)
        {
            return error;
        }

        TypeId enumType = frontend.Types.Map(result.TypeArguments[1], Location.None);
        return Emit(enumType, _f.Cast(CastKind.ErrorEnum, ArithMode.Default, error, enumType), span);
    }

    private OperandId LowerErrorName(IOperation instance)
    {
        SourceSpan span = SpanOf(instance);
        TypeId text = _builder.SliceOf(_builder.Primitive(TypeKind.U8), true);
        return Emit(text, _f.Intrinsic(ErrorNameIntrinsic, [Snapshot(LowerValue(instance), span)]), span);
    }

    private OperandId LowerTryGet(IInvocationOperation invocation)
    {
        SourceSpan span = SpanOf(invocation);
        LocalId saved = NewTemp(_builder.Primitive(TypeKind.U32));
        _f.TraceSave(saved, span);
        IOperation instance = invocation.Instance!;
        PlaceId place = PlaceOf(instance);
        PlaceId value = PlaceOf(invocation.Arguments[0].Value);
        PlaceId error = PlaceOf(invocation.Arguments[1].Value);
        LocalId outcome = NewTemp(BoolType());
        OperandId succeeded = Emit(BoolType(), _f.HasPayload(place), span);
        BlockId success = _f.NewBlock();
        BlockId failure = _f.NewBlock();
        BlockId join = _f.NewBlock();
        _f.Branch(succeeded, success, failure, span);
        _f.SetBlock(success);
        if (ReadPayload(place) is { } payload)
        {
            _f.Assign(value, _f.Use(payload), span);
        }

        _f.Assign(error, _f.Use(Zero(IrTyper.OfPlace(_module, _body, error), span)), span);
        _f.Assign(_f.LocalPlace(outcome), _f.Use(_f.Const(_builder.BoolConstant(BoolType(), true))), span);
        _f.Goto(join, span);
        _f.SetBlock(failure);
        _f.Assign(value, _f.Use(Zero(IrTyper.OfPlace(_module, _body, value), span)), span);
        _f.Assign(error, _f.Use(ReadError(place, instance.Type!, span)), span);
        _f.TraceRestore(saved, span);
        _f.Assign(_f.LocalPlace(outcome), _f.Use(_f.Const(_builder.BoolConstant(BoolType(), false))), span);
        _f.Goto(join, span);
        _f.SetBlock(join);
        return _f.Copy(_f.LocalPlace(outcome));
    }

    private void RunDefersAt(SyntaxNode position, OperandId error, SourceSpan span)
    {
        var active = new List<DeferRecord>();
        foreach (DeferRecord defer in _defers)
        {
            if (defer.Cfg == _cfg && Dominates(defer.BlockOrdinal, _currentOrdinal) && defer.Scope.Span.Contains(position.Span))
            {
                active.Add(defer);
            }
        }

        foreach (DeferRecord defer in active.Select((d, order) => (d, order)).OrderByDescending(x => x.d.Scope.SpanStart).ThenByDescending(x => x.order).Select(x => x.d))
        {
            BindErrorParameter(defer, error, span);
            InlineCfg(defer.Body, 0, defer.Body.Blocks.Length - 1, span);
        }
    }

    private void RunErrorDefer(DeferRecord defer, OperandId returned, SourceSpan span)
    {
        PlaceId place = SpillToPlace(returned, span);
        OperandId succeeded = Emit(BoolType(), _f.HasPayload(place), span);
        BlockId failure = _f.NewBlock();
        BlockId join = _f.NewBlock();
        _f.Branch(succeeded, join, failure, span);
        _f.SetBlock(failure);
        if (defer.Parameter is not null)
        {
            BindErrorParameter(defer, Emit(ErrorTypeOf(IrTyper.OfPlace(_module, _body, place)), _f.Err(place), span), span);
        }

        InlineCfg(defer.Body, 0, defer.Body.Blocks.Length - 1, span);
        _f.Goto(join, span);
        _f.SetBlock(join);
    }

    private void BindErrorParameter(DeferRecord defer, OperandId error, SourceSpan span)
    {
        if (defer.Parameter is null)
        {
            return;
        }

        TypeId anyError = _builder.AnyError();
        LocalId local = _f.NewLocal(defer.Parameter.Name, anyError, LocalKind.User, span);
        _locals[defer.Parameter] = local;
        _f.Assign(_f.LocalPlace(local), _f.Use(ConvertError(error, anyError, span)), span);
    }
}
