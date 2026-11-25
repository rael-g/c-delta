using System.Collections.Immutable;
using System.Text;
using DiagnosticCode = CDelta.Diagnostics.DiagnosticCode;
using CDelta.Ir;
using TypeKind = CDelta.Ir.TypeKind;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FlowAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace CDelta.Frontend.CSharp;

internal sealed class BodyLowerer(CSharpFrontend frontend, IMethodSymbol method)
{
    private readonly Module _module = frontend.Module;
    private readonly ModuleBuilder _builder = frontend.Builder;
    private readonly Body _body = new();
    private readonly Dictionary<ISymbol, LocalId> _locals = new(SymbolEqualityComparer.Default);
    private readonly Dictionary<CaptureId, LocalId> _captures = [];
    private readonly Dictionary<int, BlockId> _blocks = [];
    private readonly Dictionary<int, List<ILocalSymbol>> _regionLocals = [];
    private FunctionBuilder _f = null!;
    private ControlFlowGraph _cfg = null!;

    public Body Lower()
    {
        Location location = method.Locations[0];
        SyntaxNode syntax = method.DeclaringSyntaxReferences[0].GetSyntax();
        SemanticModel model = frontend.Compilation.GetSemanticModel(syntax.SyntaxTree);
        if (model.GetOperation(syntax) is not IMethodBodyOperation operation)
        {
            throw new UnsupportedConstructException("the method body has no operation tree", location);
        }

        _cfg = ControlFlowGraph.Create(operation);
        _f = new FunctionBuilder(_module, _body);
        SourceSpan methodSpan = frontend.SpanOf(location);
        foreach (IParameterSymbol parameter in method.Parameters)
        {
            _locals[parameter] = _f.NewLocal(parameter.Name, frontend.Types.Map(parameter.Type, location), LocalKind.Param, methodSpan);
        }

        CollectRegionLocals(_cfg.Root);
        foreach (BasicBlock block in _cfg.Blocks)
        {
            if (block.Kind == BasicBlockKind.Block)
            {
                _blocks[block.Ordinal] = _f.NewBlock();
            }
        }

        foreach (BasicBlock block in _cfg.Blocks)
        {
            if (block.Kind == BasicBlockKind.Block)
            {
                LowerBlock(block);
            }
        }

        if (_blocks.Count == 0)
        {
            _f.SetBlock(_f.NewBlock());
            _f.Return(OperandId.None, methodSpan);
        }

        return _body;
    }

    private static string Describe(IOperation operation) => operation.Kind + " (" + operation.Syntax + ")";

    private void CollectRegionLocals(ControlFlowRegion region)
    {
        if (region.Kind == ControlFlowRegionKind.LocalLifetime && !region.Locals.IsEmpty)
        {
            if (!_regionLocals.TryGetValue(region.FirstBlockOrdinal, out List<ILocalSymbol>? list))
            {
                list = [];
                _regionLocals[region.FirstBlockOrdinal] = list;
            }

            list.AddRange(region.Locals);
        }

        foreach (ControlFlowRegion nested in region.NestedRegions)
        {
            CollectRegionLocals(nested);
        }
    }

    private LocalId LocalOf(ILocalSymbol symbol, SourceSpan span)
    {
        if (!_locals.TryGetValue(symbol, out LocalId id))
        {
            Location location = symbol.Locations.FirstOrDefault() ?? Location.None;
            id = _f.NewLocal(symbol.Name, frontend.Types.Map(symbol.Type, location), LocalKind.User, span);
            _locals[symbol] = id;
        }

        return id;
    }

    private LocalId NewTemp(ITypeSymbol type, Location location) =>
        _f.NewLocal(null, frontend.Types.Map(type, location), LocalKind.Temp, SourceSpan.None);

    private LocalId NewTemp(TypeId type) => _f.NewLocal(null, type, LocalKind.Temp, SourceSpan.None);

    private void LowerBlock(BasicBlock block)
    {
        _f.SetBlock(_blocks[block.Ordinal]);
        SourceSpan blockSpan = frontend.SpanOf(method.Locations[0]);
        if (_regionLocals.TryGetValue(block.Ordinal, out List<ILocalSymbol>? entering))
        {
            foreach (ILocalSymbol local in entering)
            {
                _f.Live(LocalOf(local, blockSpan), blockSpan);
            }
        }

        foreach (IOperation operation in block.Operations)
        {
            LowerStatement(operation);
        }

        LowerTerminator(block);
    }

    private void LowerTerminator(BasicBlock block)
    {
        SourceSpan span = SpanOfOperation(block.BranchValue) ?? frontend.SpanOf(method.Locations[0]);
        ControlFlowBranch fallThrough = block.FallThroughSuccessor ?? throw new UnsupportedConstructException("a block has no successor", method.Locations[0]);
        if (block.ConditionKind != ControlFlowConditionKind.None)
        {
            OperandId condition = LowerValue(block.BranchValue!);
            ControlFlowBranch conditional = block.ConditionalSuccessor!;
            bool whenTrue = block.ConditionKind == ControlFlowConditionKind.WhenTrue;
            BlockId onTrue = EdgeTarget(whenTrue ? conditional : fallThrough, span);
            BlockId onFalse = EdgeTarget(whenTrue ? fallThrough : conditional, span);
            _f.Branch(condition, onTrue, onFalse, span);
            return;
        }

        if (fallThrough.Destination is { Kind: BasicBlockKind.Exit } || fallThrough.Semantics == ControlFlowBranchSemantics.Return)
        {
            OperandId? value = block.BranchValue is null ? null : LowerValue(block.BranchValue);
            if (value is not null && !fallThrough.LeavingRegions.IsEmpty)
            {
                LocalId saved = NewTemp(OperandType(value.Value));
                _f.Assign(_f.LocalPlace(saved), _f.Use(value.Value), span);
                value = _f.Copy(_f.LocalPlace(saved));
            }

            EmitDead(fallThrough, span);
            _f.Return(value ?? OperandId.None, span);
            return;
        }

        if (fallThrough.Semantics != ControlFlowBranchSemantics.Regular)
        {
            throw new UnsupportedConstructException("the control flow " + fallThrough.Semantics + " is not supported yet", method.Locations[0]);
        }

        _f.Goto(EdgeTarget(fallThrough, span), span);
    }

    private BlockId EdgeTarget(ControlFlowBranch branch, SourceSpan span)
    {
        BlockId target = _blocks[branch.Destination!.Ordinal];
        bool leavesLocals = branch.LeavingRegions.Any(r => r.Kind == ControlFlowRegionKind.LocalLifetime && !r.Locals.IsEmpty);
        if (!leavesLocals)
        {
            return target;
        }

        BlockId edge = _f.NewBlock();
        BlockId current = _f.Current;
        _f.SetBlock(edge);
        EmitDead(branch, span);
        _f.Goto(target, span);
        _f.SetBlock(current);
        return edge;
    }

    private void EmitDead(ControlFlowBranch branch, SourceSpan span)
    {
        foreach (ControlFlowRegion region in branch.LeavingRegions)
        {
            if (region.Kind != ControlFlowRegionKind.LocalLifetime)
            {
                continue;
            }

            foreach (ILocalSymbol local in region.Locals)
            {
                _f.Dead(LocalOf(local, span), span);
            }
        }
    }

    private SourceSpan? SpanOfOperation(IOperation? operation) =>
        operation is null ? null : frontend.SpanOf(operation.Syntax.GetLocation());

    private SourceSpan SpanOf(IOperation operation) => frontend.SpanOf(operation.Syntax.GetLocation());

    private TypeId OperandType(OperandId operand) => IrTyper.OfOperand(_module, _body, operand);

    private void LowerStatement(IOperation operation)
    {
        switch (operation)
        {
            case IExpressionStatementOperation statement:
                LowerExpression(statement.Operation);
                break;
            case IVariableDeclarationGroupOperation group:
                foreach (IVariableDeclarationOperation declaration in group.Declarations)
                {
                    LowerDeclaration(declaration);
                }

                break;
            case IVariableDeclarationOperation declaration:
                LowerDeclaration(declaration);
                break;
            case IFlowCaptureOperation capture:
                LowerCapture(capture);
                break;
            default:
                LowerExpression(operation);
                break;
        }
    }

    private void LowerDeclaration(IVariableDeclarationOperation declaration)
    {
        foreach (IVariableDeclaratorOperation declarator in declaration.Declarators)
        {
            SourceSpan span = SpanOf(declarator);
            LocalId local = LocalOf(declarator.Symbol, span);
            IOperation? initializer = declarator.Initializer?.Value ?? declaration.Initializer?.Value;
            if (initializer is not null)
            {
                OperandId value = LowerValue(initializer);
                _f.Assign(_f.LocalPlace(local), _f.Use(value), span);
            }
        }
    }

    private void LowerCapture(IFlowCaptureOperation capture)
    {
        OperandId value = LowerValue(capture.Value);
        if (!_captures.TryGetValue(capture.Id, out LocalId local))
        {
            local = NewTemp(OperandType(value));
            _captures[capture.Id] = local;
        }

        _f.Assign(_f.LocalPlace(local), _f.Use(value), SpanOf(capture));
    }

    private OperandId LowerValue(IOperation operation) =>
        LowerExpression(operation) ?? throw new UnsupportedConstructException("a void expression is used as a value", operation.Syntax.GetLocation());

    private OperandId? LowerExpression(IOperation operation)
    {
        Location location = operation.Syntax.GetLocation();
        if (operation.ConstantValue is { HasValue: true } constant && operation.Type is not null)
        {
            return LowerConstant(constant.Value, operation.Type, location);
        }

        switch (operation)
        {
            case ILocalReferenceOperation local:
                return _f.Copy(_f.LocalPlace(LocalOf(local.Local, SpanOf(operation))));
            case IParameterReferenceOperation parameter:
                return _f.Copy(_f.LocalPlace(_locals[parameter.Parameter]));
            case IFlowCaptureReferenceOperation reference:
                return _f.Copy(_f.LocalPlace(_captures[reference.Id]));
            case IParenthesizedOperation parenthesized:
                return LowerExpression(parenthesized.Operand);
            case IBinaryOperation binary:
                return LowerBinary(binary);
            case IUnaryOperation unary:
                return LowerUnary(unary);
            case IConversionOperation conversion:
                return LowerConversion(conversion);
            case ISimpleAssignmentOperation assignment:
                return LowerAssignment(assignment.Target, LowerValue(assignment.Value), assignment);
            case ICompoundAssignmentOperation compound:
                return LowerCompound(compound);
            case IIncrementOrDecrementOperation increment:
                return LowerIncrement(increment);
            case IInvocationOperation invocation:
                return LowerInvocation(invocation);
            default:
                throw new UnsupportedConstructException("the construct " + Describe(operation) + " is not supported yet", location);
        }
    }

    private OperandId LowerConstant(object? value, ITypeSymbol type, Location location)
    {
        TypeId typeId = frontend.Types.Map(type, location);
        if (value is null)
        {
            return _f.Const(_builder.NullConstant(typeId));
        }

        if (value is bool flag)
        {
            return _f.Const(_builder.BoolConstant(typeId, flag));
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
            _ => throw new UnsupportedConstructException("the constant of type " + type.ToDisplayString() + " is not supported yet", location),
        };
        return _f.Const(_builder.IntConstant(typeId, number));
    }

    private ArithMode ModeOf(IOperation operation)
    {
        foreach (SyntaxNode ancestor in operation.Syntax.AncestorsAndSelf())
        {
            switch (ancestor.Kind())
            {
                case SyntaxKind.CheckedExpression:
                case SyntaxKind.CheckedStatement:
                    return ArithMode.Trap;
                case SyntaxKind.UncheckedExpression:
                case SyntaxKind.UncheckedStatement:
                    return ArithMode.Wrap;
            }
        }

        return ArithMode.Default;
    }

    private OperandId Emit(TypeId type, RvalueId rvalue, SourceSpan span)
    {
        LocalId temp = NewTemp(type);
        _f.Assign(_f.LocalPlace(temp), rvalue, span);
        return _f.Copy(_f.LocalPlace(temp));
    }

    private static bool IsPure(IOperation operation) =>
        operation.ConstantValue.HasValue
        || operation is ILocalReferenceOperation or IParameterReferenceOperation or IFlowCaptureReferenceOperation
        || operation is IConversionOperation { Operand: var inner } && IsPure(inner)
        || operation is IParenthesizedOperation { Operand: var wrapped } && IsPure(wrapped);

    private OperandId Snapshot(OperandId operand, SourceSpan span)
    {
        Operand o = _body.Operands[operand.Index];
        if (o.Kind != OperandKind.Copy)
        {
            return operand;
        }

        return Emit(OperandType(operand), _f.Use(operand), span);
    }

    private static BinOp? BinaryOperator(BinaryOperatorKind kind) => kind switch
    {
        BinaryOperatorKind.Add => BinOp.Add,
        BinaryOperatorKind.Subtract => BinOp.Sub,
        BinaryOperatorKind.Multiply => BinOp.Mul,
        BinaryOperatorKind.Divide => BinOp.Div,
        BinaryOperatorKind.Remainder => BinOp.Rem,
        BinaryOperatorKind.LeftShift => BinOp.Shl,
        BinaryOperatorKind.RightShift => BinOp.Shr,
        BinaryOperatorKind.And => BinOp.And,
        BinaryOperatorKind.Or => BinOp.Or,
        BinaryOperatorKind.ExclusiveOr => BinOp.Xor,
        BinaryOperatorKind.Equals => BinOp.Eq,
        BinaryOperatorKind.NotEquals => BinOp.Ne,
        BinaryOperatorKind.LessThan => BinOp.Lt,
        BinaryOperatorKind.LessThanOrEqual => BinOp.Le,
        BinaryOperatorKind.GreaterThan => BinOp.Gt,
        BinaryOperatorKind.GreaterThanOrEqual => BinOp.Ge,
        _ => null,
    };

    private OperandId LowerBinary(IBinaryOperation binary)
    {
        Location location = binary.Syntax.GetLocation();
        BinOp op = BinaryOperator(binary.OperatorKind)
            ?? throw new UnsupportedConstructException("the operator " + binary.OperatorKind + " is not supported yet", location);
        SourceSpan span = SpanOf(binary);
        OperandId left = LowerValue(binary.LeftOperand);
        if (!IsPure(binary.RightOperand))
        {
            left = Snapshot(left, span);
        }

        OperandId right = LowerValue(binary.RightOperand);
        TypeId result = frontend.Types.Map(binary.Type!, location);
        return Emit(result, _f.Binop(op, ModeOf(binary), left, right), span);
    }

    private OperandId LowerUnary(IUnaryOperation unary)
    {
        Location location = unary.Syntax.GetLocation();
        SourceSpan span = SpanOf(unary);
        OperandId operand = LowerValue(unary.Operand);
        TypeId type = frontend.Types.Map(unary.Type!, location);
        switch (unary.OperatorKind)
        {
            case UnaryOperatorKind.Plus:
                return operand;
            case UnaryOperatorKind.Minus:
                return Emit(type, _f.Unop(UnOp.Neg, ModeOf(unary), operand), span);
            case UnaryOperatorKind.Not:
                return Emit(type, _f.Unop(UnOp.Not, ArithMode.Default, operand), span);
            case UnaryOperatorKind.BitwiseNegation:
                return Emit(type, _f.Unop(UnOp.BitNot, ArithMode.Default, operand), span);
            default:
                throw new UnsupportedConstructException("the operator " + unary.OperatorKind + " is not supported yet", location);
        }
    }

    private OperandId LowerConversion(IConversionOperation conversion)
    {
        Location location = conversion.Syntax.GetLocation();
        ITypeSymbol target = conversion.Type!;
        ITypeSymbol? source = conversion.Operand.Type;
        if (conversion.Operand.ConstantValue is { HasValue: true, Value: string text } && source?.SpecialType == SpecialType.System_String)
        {
            return LowerStringToPointer(text, target, conversion, location);
        }

        OperandId operand = LowerValue(conversion.Operand);
        if (source is null || SymbolEqualityComparer.Default.Equals(source, target))
        {
            return operand;
        }

        TypeId targetType = frontend.Types.Map(target, location);
        TypeId sourceType = frontend.Types.Map(source, location);
        if (sourceType == targetType)
        {
            return operand;
        }

        TypeKind sourceKind = _module.Types[sourceType.Index].Kind;
        TypeKind targetKind = _module.Types[targetType.Index].Kind;
        SourceSpan span = SpanOf(conversion);
        ArithMode mode = conversion.IsImplicit ? ArithMode.Wrap : ModeOf(conversion);
        if (IrTypes.IsInteger(sourceKind) && IrTypes.IsInteger(targetKind))
        {
            return Emit(targetType, _f.Cast(CastKind.IntInt, mode, operand, targetType), span);
        }

        if (sourceKind == TypeKind.Ptr && targetKind == TypeKind.Ptr)
        {
            return Emit(targetType, _f.Cast(CastKind.PtrPtr, ArithMode.Default, operand, targetType), span);
        }

        throw new UnsupportedConstructException("the conversion from " + source.ToDisplayString() + " to " + target.ToDisplayString() + " is not supported yet", location);
    }

    private OperandId LowerStringToPointer(string text, ITypeSymbol target, IConversionOperation conversion, Location location)
    {
        if (target is not IPointerTypeSymbol pointer || pointer.PointedAtType.SpecialType != SpecialType.System_Byte)
        {
            throw new UnsupportedConstructException("a string converts only to byte* at this stage", location);
        }

        byte[] encoded = new UTF8Encoding(false, true).GetBytes(text);

        TypeId u8 = _builder.Primitive(TypeKind.U8);
        TypeId constType = _builder.Pointer(u8, TypeFlags.Nullable | TypeFlags.Const | TypeFlags.Many);
        OperandId constant = _f.Const(_builder.BytesConstant(constType, encoded));
        TypeId targetType = frontend.Types.Map(target, location);
        return Emit(targetType, _f.Cast(CastKind.PtrPtr, ArithMode.Default, constant, targetType), SpanOf(conversion));
    }

    private OperandId LowerAssignment(IOperation target, OperandId value, IOperation origin)
    {
        PlaceId place = PlaceOf(target);
        _f.Assign(place, _f.Use(value), SpanOf(origin));
        return _f.Copy(place);
    }

    private PlaceId PlaceOf(IOperation target) => target switch
    {
        ILocalReferenceOperation local => _f.LocalPlace(LocalOf(local.Local, SpanOf(target))),
        IParameterReferenceOperation parameter => _f.LocalPlace(_locals[parameter.Parameter]),
        IFlowCaptureReferenceOperation reference => _f.LocalPlace(_captures[reference.Id]),
        _ => throw new UnsupportedConstructException("the assignment target " + Describe(target) + " is not supported yet", target.Syntax.GetLocation()),
    };

    private OperandId LowerCompound(ICompoundAssignmentOperation compound)
    {
        Location location = compound.Syntax.GetLocation();
        BinOp op = BinaryOperator(compound.OperatorKind)
            ?? throw new UnsupportedConstructException("the operator " + compound.OperatorKind + " is not supported yet", location);
        SourceSpan span = SpanOf(compound);
        PlaceId place = PlaceOf(compound.Target);
        OperandId current = Snapshot(_f.Copy(place), span);
        OperandId right = LowerValue(compound.Value);
        TypeId type = IrTyper.OfPlace(_module, _body, place);
        OperandId result = Emit(type, _f.Binop(op, ModeOf(compound), current, right), span);
        _f.Assign(place, _f.Use(result), span);
        return _f.Copy(place);
    }

    private OperandId LowerIncrement(IIncrementOrDecrementOperation increment)
    {
        SourceSpan span = SpanOf(increment);
        PlaceId place = PlaceOf(increment.Target);
        TypeId type = IrTyper.OfPlace(_module, _body, place);
        OperandId before = Snapshot(_f.Copy(place), span);
        OperandId one = _f.Const(_builder.IntConstant(type, 1));
        BinOp op = increment.Kind == OperationKind.Increment ? BinOp.Add : BinOp.Sub;
        OperandId updated = Emit(type, _f.Binop(op, ModeOf(increment), before, one), span);
        _f.Assign(place, _f.Use(updated), span);
        return increment.IsPostfix ? before : _f.Copy(place);
    }

    private OperandId? LowerInvocation(IInvocationOperation invocation)
    {
        Location location = invocation.Syntax.GetLocation();
        if (!invocation.TargetMethod.IsStatic)
        {
            throw new UnsupportedConstructException("instance methods are not supported yet", location);
        }

        SourceSpan span = SpanOf(invocation);
        var arguments = new List<OperandId>();
        foreach (IArgumentOperation argument in invocation.Arguments)
        {
            arguments.Add(Snapshot(LowerValue(argument.Value), span));
        }

        FunctionId callee = frontend.Declare(invocation.TargetMethod);
        RvalueId call = _f.CallDirect(callee, arguments);
        TypeId returnType = _module.Functions[callee.Index].Return;
        if (_module.Types[returnType.Index].Kind == TypeKind.Void)
        {
            _f.CallStatement(call, span);
            return null;
        }

        return Emit(returnType, call, span);
    }
}
