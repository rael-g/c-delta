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
    private const int RangeCurrent = 0;
    private const int RangeEnd = 1;
    private const int EnumeratorItems = 0;
    private const int EnumeratorIndex = 1;

    private readonly Module _module = frontend.Module;
    private readonly ModuleBuilder _builder = frontend.Builder;
    private readonly Body _body = new();
    private readonly Dictionary<ISymbol, LocalId> _locals = new(SymbolEqualityComparer.Default);
    private readonly Dictionary<(ControlFlowGraph Cfg, CaptureId Id), LocalId> _captures = [];
    private readonly Dictionary<(ControlFlowGraph Cfg, CaptureId Id), PlaceId> _captureAliases = [];
    private readonly Dictionary<ControlFlowGraph, HashSet<CaptureId>> _assignedCaptures = [];
    private readonly List<DeferRecord> _defers = [];
    private readonly Dictionary<ControlFlowGraph, List<HashSet<int>>> _dominators = [];
    private Dictionary<int, BlockId> _blocks = [];
    private Dictionary<int, List<ILocalSymbol>> _regionLocals = [];
    private BlockId _inlineExit = BlockId.None;
    private int _inlineFirst;
    private int _inlineLast;
    private LocalId _thisLocal = LocalId.None;
    private FunctionBuilder _f = null!;
    private ControlFlowGraph _cfg = null!;

    public Body Lower()
    {
        Location location = method.Locations[0];
        SyntaxNode syntax = method.DeclaringSyntaxReferences[0].GetSyntax();
        SemanticModel model = frontend.Compilation.GetSemanticModel(syntax.SyntaxTree);
        IOperation? operation = model.GetOperation(syntax);
        _cfg = operation switch
        {
            IMethodBodyOperation methodBody => ControlFlowGraph.Create(methodBody),
            IConstructorBodyOperation constructorBody => ControlFlowGraph.Create(constructorBody),
            _ => throw new UnsupportedConstructException("the method body has no operation tree", location),
        };
        _f = new FunctionBuilder(_module, _body);
        SourceSpan methodSpan = frontend.SpanOf(location);
        if (!method.IsStatic)
        {
            TypeId self = frontend.Types.Map(method.ContainingType, location);
            TypeFlags constness = frontend.IsReadOnlyMember(method) ? TypeFlags.Const : TypeFlags.None;
            _thisLocal = _f.NewLocal("this", _builder.Pointer(self, constness), LocalKind.Param, methodSpan);
        }

        foreach (IParameterSymbol parameter in method.Parameters)
        {
            _locals[parameter] = _f.NewLocal(parameter.Name, frontend.Types.ParameterType(parameter, location), LocalKind.Param, methodSpan);
        }

        CollectRegionLocals(_cfg.Root);
        foreach (BasicBlock block in _cfg.Blocks)
        {
            if (block.Kind == BasicBlockKind.Block && !InFinally(block))
            {
                _blocks[block.Ordinal] = _f.NewBlock();
            }
        }

        foreach (BasicBlock block in _cfg.Blocks)
        {
            if (block.Kind == BasicBlockKind.Block && !InFinally(block))
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

    private static bool InFinally(BasicBlock block)
    {
        for (ControlFlowRegion? region = block.EnclosingRegion; region is not null; region = region.EnclosingRegion)
        {
            if (region.Kind == ControlFlowRegionKind.Finally)
            {
                return true;
            }
        }

        return false;
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
            TypeId type = frontend.Types.ReferenceType(frontend.Types.Map(symbol.Type, location), symbol.RefKind);
            id = _f.NewLocal(symbol.Name, type, LocalKind.User, span);
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
            if (!TryRecordDefer(operation, block))
            {
                LowerStatement(operation);
            }
        }

        LowerTerminator(block);
    }

    private bool TryRecordDefer(IOperation operation, BasicBlock block)
    {
        IOperation statement = operation is IExpressionStatementOperation expression ? expression.Operation : operation;
        if (statement is not IInvocationOperation invocation
            || invocation.TargetMethod.Name != "Defer"
            || invocation.TargetMethod.ContainingType.ToDisplayString() != "CDelta.Intrinsics"
            || invocation.Arguments.Length != 1)
        {
            return false;
        }

        if (invocation.Arguments[0].Value is not IDelegateCreationOperation { Target: IFlowAnonymousFunctionOperation lambda })
        {
            throw new UnsupportedConstructException("Defer needs a lambda written in place", operation.Syntax.GetLocation());
        }

        SyntaxNode scope = invocation.Syntax.Ancestors().FirstOrDefault(n => n is BlockSyntax or SwitchSectionSyntax or ArrowExpressionClauseSyntax)
            ?? invocation.Syntax;
        _defers.Add(new DeferRecord(_cfg, scope, block.Ordinal, _cfg.GetAnonymousFunctionControlFlowGraph(lambda)));
        return true;
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
            BlockId onTrue = EdgeTarget(whenTrue ? conditional : fallThrough, block, span);
            BlockId onFalse = EdgeTarget(whenTrue ? fallThrough : conditional, block, span);
            _f.Branch(condition, onTrue, onFalse, span);
            return;
        }

        if (LeavesInlinedRegion(fallThrough))
        {
            _f.Goto(_inlineExit, span);
            return;
        }

        if (fallThrough.Destination is { Kind: BasicBlockKind.Exit } || fallThrough.Semantics == ControlFlowBranchSemantics.Return)
        {
            OperandId? value = block.BranchValue is null
                ? null
                : method.RefKind == RefKind.None ? LowerValue(block.BranchValue) : ReferenceTo(block.BranchValue, method.RefKind != RefKind.Ref, span);
            if (value is not null && (!fallThrough.LeavingRegions.IsEmpty || ActiveDefers(fallThrough, block).Count > 0))
            {
                LocalId saved = NewTemp(OperandType(value.Value));
                _f.Assign(_f.LocalPlace(saved), _f.Use(value.Value), span);
                value = _f.Copy(_f.LocalPlace(saved));
            }

            EmitExitActions(fallThrough, block, span, true);
            _f.Return(value ?? OperandId.None, span);
            return;
        }

        if (fallThrough.Semantics == ControlFlowBranchSemantics.Throw && (block.BranchValue is null || block.BranchValue.IsImplicit
            || block.BranchValue is IObjectCreationOperation { Type.Name: "SwitchExpressionException" or "InvalidOperationException" }))
        {
            _f.Unreachable(UnreachableMode.Default, span);
            return;
        }

        if (fallThrough.Semantics != ControlFlowBranchSemantics.Regular)
        {
            throw new UnsupportedConstructException("the control flow " + fallThrough.Semantics + " is not supported yet", method.Locations[0]);
        }

        _f.Goto(EdgeTarget(fallThrough, block, span), span);
    }

    private bool LeavesInlinedRegion(ControlFlowBranch branch)
    {
        if (_inlineExit.IsNone)
        {
            return false;
        }

        BasicBlock? destination = branch.Destination;
        return destination is null
            || destination.Kind == BasicBlockKind.Exit
            || destination.Ordinal < _inlineFirst
            || destination.Ordinal > _inlineLast;
    }

    private BlockId EdgeTarget(ControlFlowBranch branch, BasicBlock source, SourceSpan span)
    {
        if (LeavesInlinedRegion(branch))
        {
            return _inlineExit;
        }

        if (branch.Destination!.Kind == BasicBlockKind.Exit)
        {
            BlockId exit = _f.NewBlock();
            BlockId resume = _f.Current;
            _f.SetBlock(exit);
            EmitExitActions(branch, source, span, true);
            _f.Return(OperandId.None, span);
            _f.SetBlock(resume);
            return exit;
        }

        BlockId target = _blocks[branch.Destination.Ordinal];
        if (branch.LeavingRegions.IsEmpty && ActiveDefers(branch, source).Count == 0)
        {
            return target;
        }

        BlockId edge = _f.NewBlock();
        BlockId current = _f.Current;
        _f.SetBlock(edge);
        EmitExitActions(branch, source, span, false);
        _f.Goto(target, span);
        _f.SetBlock(current);
        return edge;
    }

    private void EmitExitActions(ControlFlowBranch branch, BasicBlock source, SourceSpan span, bool isReturn)
    {
        RunDefers(branch, source, span);
        foreach (ControlFlowRegion region in branch.LeavingRegions)
        {
            switch (region.Kind)
            {
                case ControlFlowRegionKind.Try when region.EnclosingRegion is { Kind: ControlFlowRegionKind.TryAndFinally } protectedRegion:
                    ControlFlowRegion finallyRegion = protectedRegion.NestedRegions.First(r => r.Kind == ControlFlowRegionKind.Finally);
                    InlineFinally(finallyRegion, span);
                    break;
                case ControlFlowRegionKind.LocalLifetime:
                    foreach (ILocalSymbol local in region.Locals)
                    {
                        _f.Dead(LocalOf(local, span), span);
                    }

                    break;
            }
        }
    }

    private static SyntaxNode? PositionOf(BasicBlock block, int depth = 0)
    {
        SyntaxNode? own = block.Operations.LastOrDefault()?.Syntax ?? block.BranchValue?.Syntax;
        if (own is not null || depth > 8)
        {
            return own;
        }

        BasicBlock? next = block.FallThroughSuccessor?.Destination;
        return next is null || next.Kind == BasicBlockKind.Exit ? null : PositionOf(next, depth + 1);
    }

    private void RunDefers(ControlFlowBranch branch, BasicBlock source, SourceSpan span)
    {
        foreach (DeferRecord defer in ActiveDefers(branch, source))
        {
            InlineCfg(defer.Body, 0, defer.Body.Blocks.Length - 1, span);
        }
    }

    private List<DeferRecord> ActiveDefers(ControlFlowBranch branch, BasicBlock source)
    {
        SyntaxNode? from = PositionOf(source);
        SyntaxNode? to = branch.Destination is null || branch.Destination.Kind == BasicBlockKind.Exit ? null : PositionOf(branch.Destination);
        var active = new List<DeferRecord>();
        foreach (DeferRecord defer in _defers)
        {
            if (defer.Cfg != _cfg || !Dominates(defer.BlockOrdinal, source.Ordinal))
            {
                continue;
            }

            bool insideBefore = from is null || defer.Scope.Span.Contains(from.Span);
            bool insideAfter = to is not null && defer.Scope.Span.Contains(to.Span);
            if (insideBefore && !insideAfter)
            {
                active.Add(defer);
            }
        }

        active = [.. active.Select((d, order) => (d, order)).OrderByDescending(x => x.d.Scope.SpanStart).ThenByDescending(x => x.order).Select(x => x.d)];
        return active;
    }

    private bool Dominates(int candidate, int block)
    {
        if (!_dominators.TryGetValue(_cfg, out List<HashSet<int>>? sets))
        {
            sets = ComputeDominators(_cfg);
            _dominators[_cfg] = sets;
        }

        return sets[block].Contains(candidate);
    }

    private static List<HashSet<int>> ComputeDominators(ControlFlowGraph cfg)
    {
        int count = cfg.Blocks.Length;
        var predecessors = new List<int>[count];
        for (int i = 0; i < count; i++)
        {
            predecessors[i] = [];
        }

        foreach (BasicBlock block in cfg.Blocks)
        {
            foreach (BasicBlock? next in new[] { block.FallThroughSuccessor?.Destination, block.ConditionalSuccessor?.Destination })
            {
                if (next is not null)
                {
                    predecessors[next.Ordinal].Add(block.Ordinal);
                }
            }
        }

        var all = Enumerable.Range(0, count).ToHashSet();
        var sets = new List<HashSet<int>>();
        for (int i = 0; i < count; i++)
        {
            sets.Add(i == 0 ? [0] : new HashSet<int>(all));
        }

        bool changed = true;
        while (changed)
        {
            changed = false;
            for (int i = 1; i < count; i++)
            {
                HashSet<int>? intersection = null;
                foreach (int predecessor in predecessors[i])
                {
                    intersection = intersection is null ? new HashSet<int>(sets[predecessor]) : [.. intersection.Intersect(sets[predecessor])];
                }

                intersection ??= [];
                intersection.Add(i);
                if (!intersection.SetEquals(sets[i]))
                {
                    sets[i] = intersection;
                    changed = true;
                }
            }
        }

        return sets;
    }

    private void InlineFinally(ControlFlowRegion region, SourceSpan span)
    {
        if (!DisposesThroughInterface(region))
        {
            InlineCfg(_cfg, region.FirstBlockOrdinal, region.LastBlockOrdinal, span);
        }
    }

    private bool DisposesThroughInterface(ControlFlowRegion region) =>
        _cfg.Blocks
            .Where(block => block.Ordinal >= region.FirstBlockOrdinal && block.Ordinal <= region.LastBlockOrdinal)
            .SelectMany(block => block.Operations.Concat(block.BranchValue is null ? [] : [block.BranchValue]))
            .SelectMany(operation => operation.DescendantsAndSelf())
            .Any(operation => operation.Type is { Name: "IDisposable", TypeKind: Microsoft.CodeAnalysis.TypeKind.Interface });

    private void InlineCfg(ControlFlowGraph cfg, int first, int last, SourceSpan span)
    {
        BlockId continuation = _f.NewBlock();
        ControlFlowGraph savedCfg = _cfg;
        Dictionary<int, BlockId> savedBlocks = _blocks;
        Dictionary<int, List<ILocalSymbol>> savedRegionLocals = _regionLocals;
        BlockId savedExit = _inlineExit;
        int savedFirst = _inlineFirst;
        int savedLast = _inlineLast;

        _cfg = cfg;
        _blocks = new Dictionary<int, BlockId>(cfg == savedCfg ? savedBlocks : []);
        _regionLocals = cfg == savedCfg ? savedRegionLocals : [];
        if (cfg != savedCfg)
        {
            CollectRegionLocals(cfg.Root);
        }

        _inlineExit = continuation;
        _inlineFirst = first;
        _inlineLast = last;
        var inlined = new List<BasicBlock>();
        foreach (BasicBlock block in cfg.Blocks)
        {
            if (block.Kind == BasicBlockKind.Block && block.Ordinal >= first && block.Ordinal <= last)
            {
                _blocks[block.Ordinal] = _f.NewBlock();
                inlined.Add(block);
            }
        }

        _f.Goto(_blocks[inlined.Count == 0 ? first : inlined[0].Ordinal], span);
        foreach (BasicBlock block in inlined)
        {
            LowerBlock(block);
        }

        _cfg = savedCfg;
        _blocks = savedBlocks;
        _regionLocals = savedRegionLocals;
        _inlineExit = savedExit;
        _inlineFirst = savedFirst;
        _inlineLast = savedLast;
        _f.SetBlock(continuation);
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
        if (AssignedCaptures().Contains(capture.Id) && capture.Value is ILocalReferenceOperation or IParameterReferenceOperation or IFieldReferenceOperation or IArrayElementReferenceOperation)
        {
            _captureAliases[(_cfg, capture.Id)] = PlaceOf(capture.Value);
            return;
        }

        OperandId value = LowerValue(capture.Value);
        if (!_captures.TryGetValue((_cfg, capture.Id), out LocalId local))
        {
            local = NewTemp(OperandType(value));
            _captures[(_cfg, capture.Id)] = local;
        }

        _f.Assign(_f.LocalPlace(local), _f.Use(value), SpanOf(capture));
    }

    private HashSet<CaptureId> AssignedCaptures()
    {
        if (_assignedCaptures.TryGetValue(_cfg, out HashSet<CaptureId>? known))
        {
            return known;
        }

        var ids = new HashSet<CaptureId>();
        foreach (BasicBlock block in _cfg.Blocks)
        {
            foreach (IOperation root in block.Operations.Concat(block.BranchValue is null ? [] : [block.BranchValue]))
            {
                foreach (IOperation node in root.DescendantsAndSelf())
                {
                    if (node is IAssignmentOperation { Target: IFlowCaptureReferenceOperation target })
                    {
                        ids.Add(target.Id);
                    }
                }
            }
        }

        _assignedCaptures[_cfg] = ids;
        return ids;
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
            case IPropertyReferenceOperation length when length.Property.Name == "Length"
                && (length.Instance?.Type is IArrayTypeSymbol || TypeMapper.IsSpanLike(length.Instance?.Type)):
                return Emit(_builder.Primitive(TypeKind.I32), _f.Len(PlaceOf(length.Instance!)), SpanOf(operation));
            case IFieldReferenceOperation { Field.IsFixedSizeBuffer: true } buffer:
                PlaceId bufferPlace = PlaceOf(buffer);
                TypeId bufferType = IrTyper.OfPlace(_module, _body, bufferPlace);
                return AddressOfFirst(bufferPlace, new TypeId(_module.Types[bufferType.Index].A), frontend.Types.Map(buffer.Type!, location), SpanOf(operation));
            case ILocalReferenceOperation or IParameterReferenceOperation or IFlowCaptureReferenceOperation
                or IFieldReferenceOperation or IInstanceReferenceOperation or IDeclarationExpressionOperation
                or IArrayElementReferenceOperation:
                return _f.Copy(PlaceOf(operation));
            case IPropertyReferenceOperation { Property.IsIndexer: true } when TypeMapper.IsSpanLike(((IPropertyReferenceOperation)operation).Instance?.Type):
                return _f.Copy(PlaceOf(operation));
            case IOperation { Kind: OperationKind.ImplicitIndexerReference } rangeSlice when IsRangeAccess(rangeSlice):
                return LowerRangeSlice(rangeSlice);
            case IOperation { Kind: OperationKind.ImplicitIndexerReference }:
                return _f.Copy(PlaceOf(operation));
            case IInlineArrayAccessOperation:
                return _f.Copy(PlaceOf(operation));
            case IIsNullOperation isNull:
                return LowerIsNull(isNull);
            case ICollectionExpressionOperation collection:
                return LowerCollectionExpression(collection);
            case IInvocationOperation { TargetMethod: { Name: "GetEnumerator", ContainingType.Name: "RangeExtensions" } } rangeEnumeration:
                return LowerRangeEnumerator(rangeEnumeration);
            case IInvocationOperation { TargetMethod: { Name: "MoveNext", ContainingType.Name: "RangeEnumerator" } } rangeAdvance:
                return LowerRangeMoveNext(rangeAdvance);
            case IPropertyReferenceOperation { Property: { Name: "Current", ContainingType.Name: "RangeEnumerator" } } rangeCurrent:
                return _f.Copy(_f.Project(PlaceOf(rangeCurrent.Instance!), ProjectionKind.Field, RangeCurrent));
            case IInvocationOperation { TargetMethod: { Name: "GetEnumerator", ContainingType.Name: "IEnumerable" } } arrayEnumeration:
                return LowerArrayEnumerator(arrayEnumeration);
            case IInvocationOperation { TargetMethod: { Name: "MoveNext", ContainingType.Name: "IEnumerator" } } arrayAdvance:
                return LowerMoveNext(arrayAdvance);
            case IPropertyReferenceOperation { Property: { Name: "Current", ContainingType.Name: "IEnumerator" } }:
                return _f.Copy(PlaceOf(operation));
            case IInvocationOperation { TargetMethod.Name: "GetEnumerator" } enumeration when TypeMapper.IsSpanLike(enumeration.TargetMethod.ContainingType):
                return LowerGetEnumerator(enumeration);
            case IInvocationOperation { TargetMethod.Name: "MoveNext" } advance when advance.TargetMethod.ContainingType is { Name: "Enumerator", ContainingType: { } owner } && TypeMapper.IsSpanLike(owner):
                return LowerMoveNext(advance);
            case IPropertyReferenceOperation { Property.Name: "Current" } current when current.Property.ContainingType is { Name: "Enumerator", ContainingType: { } outer } && TypeMapper.IsSpanLike(outer):
                return _f.Copy(PlaceOf(operation));
            case IOperation { Kind: OperationKind.None } fixedInitializer when fixedInitializer.Syntax.Parent is EqualsValueClauseSyntax { Parent.Parent.Parent: FixedStatementSyntax }:
                return LowerFixedInitializer(fixedInitializer);
            case IOperation { Kind: OperationKind.None } stackAllocation when stackAllocation.Syntax is StackAllocArrayCreationExpressionSyntax:
                return LowerStackAlloc(stackAllocation);
            case IPropertyReferenceOperation { Instance: not null } nullableMember when TypeMapper.IsNullable(nullableMember.Instance.Type):
                return LowerNullableMember(nullableMember);
            case IConversionOperation { Operand.Type: not null } fromNullable when TypeMapper.IsNullable(fromNullable.Operand.Type) && !TypeMapper.IsNullable(fromNullable.Type):
                return _f.Copy(_f.Project(PlaceOf(fromNullable.Operand), ProjectionKind.Payload, 0));
            case IOperation { Kind: OperationKind.None } when IsPointerForm(operation):
                return _f.Copy(PlaceOf(operation));
            case ITupleOperation tuple:
                return LowerTuple(tuple);
            case IDeconstructionAssignmentOperation deconstruction:
                return LowerDeconstruction(deconstruction);
            case IIsPatternOperation isPattern:
                return TestPattern(isPattern.Pattern, PlaceOf(isPattern.Value), SpanOf(operation));
            case IObjectCreationOperation creation:
                return LowerObjectCreation(creation);
            case IDefaultValueOperation when TypeMapper.IsNullable(operation.Type):
                TypeId none = frontend.Types.Map(operation.Type!, location);
                return Emit(none, _f.OptionalNone(new TypeId(_module.Types[none.Index].A)), SpanOf(operation));
            case IDefaultValueOperation:
                return Zero(frontend.Types.Map(operation.Type!, location), SpanOf(operation));
            case IAddressOfOperation address:
                return LowerAddressOf(address);
            case ISizeOfOperation sizeOf:
                return Emit(_builder.Primitive(TypeKind.I32), _f.SizeOf(frontend.Types.Map(sizeOf.TypeOperand, location)), SpanOf(operation));
            case IParenthesizedOperation parenthesized:
                return LowerExpression(parenthesized.Operand);
            case IBinaryOperation binary:
                return LowerBinary(binary);
            case IUnaryOperation unary:
                return LowerUnary(unary);
            case IConversionOperation conversion:
                return LowerConversion(conversion);
            case ISimpleAssignmentOperation { IsRef: true } refAssignment:
                return LowerRefAssignment(refAssignment);
            case ISimpleAssignmentOperation assignment:
                return LowerAssignment(assignment.Target, LowerValue(assignment.Value), assignment);
            case ICompoundAssignmentOperation compound:
                return LowerCompound(compound);
            case IIncrementOrDecrementOperation increment:
                return LowerIncrement(increment);
            case IInvocationOperation { TargetMethod.RefKind: not RefKind.None } returnsReference:
                return _f.Copy(PlaceOf(returnsReference));
            case IInvocationOperation invocation:
                return LowerInvocation(invocation);
            default:
                throw new UnsupportedConstructException("the construct " + Describe(operation) + " is not supported yet", location);
        }
    }

    private OperandId LowerConstant(object? value, ITypeSymbol type, Location location)
    {
        TypeId typeId = frontend.Types.Map(type, location);
        if (TypeMapper.IsNullable(type))
        {
            SourceSpan constantSpan = frontend.SpanOf(location);
            TypeId payloadType = new(_module.Types[typeId.Index].A);
            return value is null
                ? Emit(typeId, _f.OptionalNone(payloadType), constantSpan)
                : Emit(typeId, _f.OptionalSome(LowerConstant(value, ((INamedTypeSymbol)type).TypeArguments[0], location)), constantSpan);
        }

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

    private static bool IsNullLiteral(IOperation operation) =>
        operation.ConstantValue is { HasValue: true, Value: null }
        || operation is IConversionOperation { Operand: var inner } && IsNullLiteral(inner);

    private OperandId? LowerNullComparison(IBinaryOperation binary)
    {
        if (binary.OperatorKind is not (BinaryOperatorKind.Equals or BinaryOperatorKind.NotEquals))
        {
            return null;
        }

        IOperation? nullable = TypeMapper.IsNullable(binary.LeftOperand.Type) && IsNullLiteral(binary.RightOperand) ? binary.LeftOperand
            : TypeMapper.IsNullable(binary.RightOperand.Type) && IsNullLiteral(binary.LeftOperand) ? binary.RightOperand
            : null;
        if (nullable is null)
        {
            return null;
        }

        SourceSpan span = SpanOf(binary);
        TypeId boolean = _builder.Primitive(TypeKind.Bool);
        OperandId present = Emit(boolean, _f.HasPayload(PlaceOf(nullable)), span);
        return binary.OperatorKind == BinaryOperatorKind.NotEquals ? present : Emit(boolean, _f.Unop(UnOp.Not, ArithMode.Default, present), span);
    }

    private OperandId LowerBinary(IBinaryOperation binary)
    {
        if (LowerNullComparison(binary) is { } comparison)
        {
            return comparison;
        }

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
        bool leftPointer = IsPointer(left);
        bool rightPointer = IsPointer(right);
        if (leftPointer || rightPointer)
        {
            return LowerPointerBinary(binary, op, left, right, span);
        }

        TypeId result = frontend.Types.Map(binary.Type!, location);
        return Emit(result, _f.Binop(op, ModeOf(binary), left, right), span);
    }

    private bool IsPointer(OperandId operand)
    {
        TypeId type = OperandType(operand);
        return !type.IsNone && _module.Types[type.Index].Kind == TypeKind.Ptr;
    }

    private OperandId LowerPointerBinary(IBinaryOperation binary, BinOp op, OperandId left, OperandId right, SourceSpan span)
    {
        if (op >= BinOp.Eq)
        {
            return Emit(_builder.Primitive(TypeKind.Bool), _f.Binop(op, ArithMode.Default, left, right), span);
        }

        if (IsPointer(left) && IsPointer(right))
        {
            OperandId difference = Emit(_builder.Primitive(TypeKind.ISize), _f.Binop(BinOp.Sub, ArithMode.Default, left, right), span);
            TypeId result = frontend.Types.Map(binary.Type!, binary.Syntax.GetLocation());
            return result == OperandType(difference)
                ? difference
                : Emit(result, _f.Cast(CastKind.IntInt, ArithMode.Wrap, difference, result), span);
        }

        OperandId pointer = IsPointer(left) ? left : right;
        OperandId count = ToIndexType(IsPointer(left) ? right : left, span);
        return Emit(OperandType(pointer), _f.Binop(op, ArithMode.Default, pointer, count), span);
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
        if (source is null && TypeMapper.IsNullable(target) && conversion.Operand.ConstantValue is { HasValue: true, Value: null })
        {
            TypeId optional = frontend.Types.Map(target, location);
            return Emit(optional, _f.OptionalNone(new TypeId(_module.Types[optional.Index].A)), SpanOf(conversion));
        }

        if (source is null && conversion.Operand.ConstantValue is { HasValue: true, Value: null })
        {
            return _f.Const(_builder.NullConstant(frontend.Types.Map(target, location)));
        }

        if (conversion.Operand.ConstantValue is { HasValue: true, Value: string text } && source?.SpecialType == SpecialType.System_String)
        {
            return LowerStringToPointer(text, target, conversion, location);
        }

        if (conversion.Operand is IPropertyReferenceOperation { Property: { Name: "Current", ContainingType.Name: "IEnumerator" } })
        {
            return _f.Copy(PlaceOf(conversion.Operand));
        }

        if (source is not null && TypeMapper.IsSpanLike(target) && TypeMapper.IsInlineArray(source, out int inlineLength, out _))
        {
            PlaceId storage = _f.Project(PlaceOf(conversion.Operand), ProjectionKind.Field, 0);
            TypeId i32 = _builder.Primitive(TypeKind.I32);
            OperandId whole = _f.Const(_builder.IntConstant(i32, inlineLength));
            return Emit(frontend.Types.Map(target, location), _f.Slice(storage, _f.Const(_builder.IntConstant(i32, 0)), whole), SpanOf(conversion));
        }

        if (source is not null && TypeMapper.IsSpanLike(target) && (source is IArrayTypeSymbol || TypeMapper.IsSpanLike(source)) && !SymbolEqualityComparer.Default.Equals(source, target))
        {
            return LowerSliceOfWhole(conversion.Operand, frontend.Types.Map(target, location), SpanOf(conversion));
        }

        OperandId operand = LowerValue(conversion.Operand);
        if (source is null || SymbolEqualityComparer.Default.Equals(source, target))
        {
            return operand;
        }

        if (TypeMapper.IsNullable(target) && !TypeMapper.IsNullable(source))
        {
            return Emit(frontend.Types.Map(target, location), _f.OptionalSome(operand), SpanOf(conversion));
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
        if (sourceKind == TypeKind.Enum || targetKind == TypeKind.Enum)
        {
            return LowerEnumConversion(operand, sourceType, targetType, conversion, span);
        }

        if (IrTypes.IsInteger(sourceKind) && IrTypes.IsInteger(targetKind))
        {
            return Emit(targetType, _f.Cast(CastKind.IntInt, mode, operand, targetType), span);
        }

        if (sourceKind == TypeKind.Ptr && targetKind == TypeKind.Ptr)
        {
            return Emit(targetType, _f.Cast(CastKind.PtrPtr, ArithMode.Default, operand, targetType), span);
        }

        if (sourceKind == TypeKind.Ptr && IrTypes.IsInteger(targetKind))
        {
            return Emit(targetType, _f.Cast(CastKind.PtrInt, ArithMode.Default, operand, targetType), span);
        }

        if (IrTypes.IsInteger(sourceKind) && targetKind == TypeKind.Ptr)
        {
            return Emit(targetType, _f.Cast(CastKind.IntPtr, ArithMode.Default, operand, targetType), span);
        }

        throw new UnsupportedConstructException("the conversion from " + source.ToDisplayString() + " to " + target.ToDisplayString() + " is not supported yet", location);
    }

    private OperandId LowerEnumConversion(OperandId operand, TypeId sourceType, TypeId targetType, IConversionOperation conversion, SourceSpan span)
    {
        TypeKind sourceKind = _module.Types[sourceType.Index].Kind;
        TypeKind targetKind = _module.Types[targetType.Index].Kind;
        if (sourceKind == TypeKind.Enum)
        {
            TypeId underlying = _module.Enums[_module.Types[sourceType.Index].A].Underlying;
            operand = Emit(underlying, _f.Cast(CastKind.EnumInt, ArithMode.Wrap, operand, underlying), span);
            sourceType = underlying;
        }

        if (targetKind != TypeKind.Enum)
        {
            return sourceType == targetType
                ? operand
                : Emit(targetType, _f.Cast(CastKind.IntInt, conversion.IsImplicit ? ArithMode.Wrap : ModeOf(conversion), operand, targetType), span);
        }

        TypeId targetUnderlying = _module.Enums[_module.Types[targetType.Index].A].Underlying;
        if (sourceType != targetUnderlying)
        {
            operand = Emit(targetUnderlying, _f.Cast(CastKind.IntInt, conversion.IsImplicit ? ArithMode.Wrap : ModeOf(conversion), operand, targetUnderlying), span);
        }

        return Emit(targetType, _f.Cast(CastKind.IntEnum, conversion.IsImplicit ? ArithMode.Wrap : ModeOf(conversion), operand, targetType), span);
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

    private static bool IsPointerForm(IOperation operation) =>
        operation.Syntax.IsKind(SyntaxKind.PointerIndirectionExpression)
        || operation.Syntax.IsKind(SyntaxKind.ElementAccessExpression)
        || operation.Syntax.Parent?.IsKind(SyntaxKind.PointerMemberAccessExpression) == true;

    private PlaceId PlaceOf(IOperation target)
    {
        switch (target)
        {
            case ILocalReferenceOperation local:
                return ValuePlace(LocalOf(local.Local, SpanOf(target)), local.Local.RefKind);
            case IParameterReferenceOperation parameter:
                return ValuePlace(_locals[parameter.Parameter], parameter.Parameter.RefKind);
            case IFlowCaptureReferenceOperation reference:
                return _captureAliases.TryGetValue((_cfg, reference.Id), out PlaceId alias)
                    ? alias
                    : _f.LocalPlace(_captures[(_cfg, reference.Id)]);
            case IParenthesizedOperation parenthesized:
                return PlaceOf(parenthesized.Operand);
            case IInstanceReferenceOperation:
                return _f.DerefPlace(_thisLocal);
            case IDeclarationExpressionOperation declaration:
                return PlaceOf(declaration.Expression);
            case IInvocationOperation { TargetMethod.RefKind: not RefKind.None } returnsReference:
                OperandId pointer = LowerInvocation(returnsReference)!.Value;
                LocalId held = NewTemp(OperandType(pointer));
                _f.Assign(_f.LocalPlace(held), _f.Use(pointer), SpanOf(returnsReference));
                return _f.DerefPlace(held);
            case IDiscardOperation discard:
                return _f.LocalPlace(NewTemp(discard.Type!, discard.Syntax.GetLocation()));
            case IOperation { Kind: OperationKind.ImplicitIndexerReference } implicitIndexer:
                return ImplicitElementPlace(implicitIndexer);
            case IInlineArrayAccessOperation inline:
                PlaceId storage = _f.Project(PlaceOf(inline.Instance), ProjectionKind.Field, 0);
                return _f.Project(storage, ProjectionKind.Index, LowerValue(inline.Argument).Index);
            case IPropertyReferenceOperation { Property: { Name: "Current" } } current when IsEnumeratorMember(current.Property.ContainingType):
                PlaceId enumerator = PlaceOf(current.Instance!);
                PlaceId items = _f.Project(enumerator, ProjectionKind.Field, EnumeratorItems);
                OperandId at = _f.Copy(_f.Project(enumerator, ProjectionKind.Field, EnumeratorIndex));
                return _f.Project(items, ProjectionKind.Index, at.Index);
            case IPropertyReferenceOperation { Property.IsIndexer: true } indexer when TypeMapper.IsSpanLike(indexer.Instance?.Type):
                PlaceId container = PlaceOf(indexer.Instance!);
                OperandId currentIndex = LowerValue(indexer.Arguments[0].Value);
                return _f.Project(container, ProjectionKind.Index, currentIndex.Index);
            case IPropertyReferenceOperation { Property.Name: "Value" } payload when TypeMapper.IsNullable(payload.Instance?.Type):
                return _f.Project(PlaceOf(payload.Instance!), ProjectionKind.Payload, 0);
            case IArrayElementReferenceOperation element:
                PlaceId array = PlaceOf(element.ArrayReference);
                OperandId position = LowerValue(element.Indices[0]);
                return _f.Project(array, ProjectionKind.Index, position.Index);
            case IFieldReferenceOperation field when field.Field.IsStatic:
                return _f.GlobalPlace(frontend.DeclareGlobal(field.Field));
            case IFieldReferenceOperation field:
                PlaceId owner = PlaceOf(field.Instance!);
                int index = frontend.Types.FieldIndex((INamedTypeSymbol)field.Field.ContainingType, field.Field);
                return _f.Project(owner, ProjectionKind.Field, index);
            case IOperation { Kind: OperationKind.None } when target.Syntax.IsKind(SyntaxKind.PointerIndirectionExpression)
                || target.Syntax.Parent?.IsKind(SyntaxKind.PointerMemberAccessExpression) == true:
                return _f.DerefPlace(PointerLocal(target.ChildOperations.First()));
            case IOperation { Kind: OperationKind.None } when target.Syntax.IsKind(SyntaxKind.ElementAccessExpression):
                return ElementThroughPointer(target);
            default:
                OperandId value = LowerValue(target);
                LocalId spill = NewTemp(OperandType(value));
                _f.Assign(_f.LocalPlace(spill), _f.Use(value), SpanOf(target));
                return _f.LocalPlace(spill);
        }
    }

    private PlaceId ValuePlace(LocalId local, RefKind kind) =>
        kind == RefKind.None ? _f.LocalPlace(local) : _f.DerefPlace(local);

    private LocalId PointerLocal(IOperation pointer)
    {
        OperandId value = LowerValue(pointer);
        Operand operand = _body.Operands[value.Index];
        if (operand.Kind == OperandKind.Copy)
        {
            Place place = _body.Places[operand.A];
            if (place.Base == PlaceBase.Local && place.Projections.Count == 0)
            {
                return new LocalId(place.Id);
            }
        }

        LocalId spill = NewTemp(OperandType(value));
        _f.Assign(_f.LocalPlace(spill), _f.Use(value), SpanOf(pointer));
        return spill;
    }

    private PlaceId ElementThroughPointer(IOperation access)
    {
        IOperation[] children = access.ChildOperations.ToArray();
        SourceSpan span = SpanOf(access);
        OperandId pointer = LowerValue(children[0]);
        OperandId offset = ToIndexType(LowerValue(children[1]), span);
        TypeId pointerType = OperandType(pointer);
        OperandId moved = Emit(pointerType, _f.Binop(BinOp.Add, ArithMode.Default, pointer, offset), span);
        Operand operand = _body.Operands[moved.Index];
        return _f.DerefPlace(new LocalId(_body.Places[operand.A].Id));
    }

    private OperandId ToIndexType(OperandId value, SourceSpan span)
    {
        TypeId isize = _builder.Primitive(TypeKind.ISize);
        return OperandType(value) == isize ? value : Emit(isize, _f.Cast(CastKind.IntInt, ArithMode.Wrap, value, isize), span);
    }

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
        SourceSpan span = SpanOf(invocation);
        IMethodSymbol target = invocation.TargetMethod;
        if (target.ContainingType.TypeKind == Microsoft.CodeAnalysis.TypeKind.Interface
            && invocation.Instance?.Type is { TypeKind: Microsoft.CodeAnalysis.TypeKind.Struct } receiver
            && receiver.FindImplementationForInterfaceMember(target) is IMethodSymbol implementation)
        {
            target = implementation;
        }

        if (TypeMapper.IsSpanLike(target.ContainingType) && target.Name == "Slice")
        {
            return LowerSlice(invocation, span);
        }

        if (TypeMapper.IsNullable(target.ContainingType))
        {
            switch (target.Name)
            {
                case "GetValueOrDefault":
                    return LowerGetValueOrDefault(invocation, span);
                case "get_HasValue":
                    return Emit(_builder.Primitive(TypeKind.Bool), _f.HasPayload(PlaceOf(invocation.Instance!)), span);
                case "get_Value":
                    return _f.Copy(_f.Project(PlaceOf(invocation.Instance!), ProjectionKind.Payload, 0));
            }
        }

        var arguments = new List<OperandId>();
        if (!target.IsStatic)
        {
            arguments.Add(ReceiverAddress(invocation.Instance!, frontend.IsReadOnlyMember(target), span));
        }

        foreach (IArgumentOperation argument in invocation.Arguments)
        {
            arguments.Add(LowerArgument(argument, span));
        }

        FunctionId callee = frontend.Declare(target);
        RvalueId call = _f.CallDirect(callee, arguments);
        TypeId returnType = _module.Functions[callee.Index].Return;
        if (_module.Types[returnType.Index].Kind == TypeKind.Void)
        {
            _f.CallStatement(call, span);
            return null;
        }

        return Emit(returnType, call, span);
    }

    private OperandId ReceiverAddress(IOperation instance, bool readOnly, SourceSpan span)
    {
        PlaceId place = PlaceOf(instance);
        TypeId pointee = IrTyper.OfPlace(_module, _body, place);
        TypeId pointer = _builder.Pointer(pointee, readOnly ? TypeFlags.Const : TypeFlags.None);
        return Emit(pointer, _f.Addr(place, !readOnly), span);
    }

    private OperandId LowerArgument(IArgumentOperation argument, SourceSpan span)
    {
        RefKind kind = argument.Parameter?.RefKind ?? RefKind.None;
        if (kind == RefKind.None)
        {
            return Snapshot(LowerValue(argument.Value), span);
        }

        PlaceId place = PlaceOf(argument.Value);
        bool mutable = kind is RefKind.Ref or RefKind.Out;
        TypeId pointee = IrTyper.OfPlace(_module, _body, place);
        TypeId pointer = _builder.Pointer(pointee, mutable ? TypeFlags.None : TypeFlags.Const);
        return Emit(pointer, _f.Addr(place, mutable), span);
    }

    private OperandId LowerObjectCreation(IObjectCreationOperation creation)
    {
        Location location = creation.Syntax.GetLocation();
        SourceSpan span = SpanOf(creation);
        TypeId type = frontend.Types.Map(creation.Type!, location);
        IMethodSymbol? constructor = creation.Constructor;
        if (constructor is not null && TypeMapper.IsSpanLike(constructor.ContainingType) && creation.Arguments.Length == 2)
        {
            return LowerSpanFromPointer(creation, type, span);
        }

        if (constructor is not null && TypeMapper.IsNullable(constructor.ContainingType) && creation.Arguments.Length == 1)
        {
            return Emit(type, _f.OptionalSome(LowerValue(creation.Arguments[0].Value)), span);
        }

        if (constructor is null || constructor.IsImplicitlyDeclared)
        {
            return Zero(type, span);
        }

        LocalId temp = NewTemp(type);
        _f.Assign(_f.LocalPlace(temp), _f.Use(Zero(type, span)), span);
        TypeId pointer = _builder.Pointer(type, TypeFlags.None);
        var arguments = new List<OperandId> { Emit(pointer, _f.Addr(_f.LocalPlace(temp), true), span) };
        foreach (IArgumentOperation argument in creation.Arguments)
        {
            arguments.Add(LowerArgument(argument, span));
        }

        _f.CallStatement(_f.CallDirect(frontend.Declare(constructor), arguments), span);
        return _f.Copy(_f.LocalPlace(temp));
    }

    private OperandId LowerSpanFromPointer(IObjectCreationOperation creation, TypeId type, SourceSpan span)
    {
        IOperation source = creation.Arguments[0].Value;
        while (source is IConversionOperation { Operand.Type: IPointerTypeSymbol } wrapped)
        {
            source = wrapped.Operand;
        }

        TypeNode slice = _module.Types[type.Index];
        TypeFlags flags = TypeFlags.Nullable | TypeFlags.Many | (slice.Flags & TypeFlags.Const);
        TypeId pointerType = _builder.Pointer(new TypeId(slice.A), flags);
        OperandId pointer = LowerValue(source);
        if (OperandType(pointer) != pointerType)
        {
            pointer = Emit(pointerType, _f.Cast(CastKind.PtrPtr, ArithMode.Default, pointer, pointerType), span);
        }

        OperandId length = Snapshot(LowerValue(creation.Arguments[1].Value), span);
        return Emit(type, _f.Aggregate(type, [pointer, length]), span);
    }

    private OperandId LowerSlice(IInvocationOperation invocation, SourceSpan span)
    {
        TypeId type = frontend.Types.Map(invocation.Type!, invocation.Syntax.GetLocation());
        PlaceId place = PlaceOf(invocation.Instance!);
        TypeId i32 = _builder.Primitive(TypeKind.I32);
        OperandId start = Snapshot(LowerValue(invocation.Arguments[0].Value), span);
        OperandId end = invocation.Arguments.Length == 2
            ? Emit(i32, _f.Binop(BinOp.Add, ArithMode.Default, start, Snapshot(LowerValue(invocation.Arguments[1].Value), span)), span)
            : Emit(i32, _f.Len(place), span);
        return Emit(type, _f.Slice(place, start, end), span);
    }

    private static bool IsRangeAccess(IOperation access) => access.ChildOperations.Last() is IRangeOperation;

    private PlaceId ImplicitElementPlace(IOperation access)
    {
        IOperation[] children = access.ChildOperations.ToArray();
        SourceSpan span = SpanOf(access);
        PlaceId container = PlaceOf(children[0]);
        OperandId position = FromEnd(children[1], container, span) ?? LowerValue(children[1]);
        return _f.Project(container, ProjectionKind.Index, position.Index);
    }

    private OperandId? FromEnd(IOperation index, PlaceId container, SourceSpan span)
    {
        if (index is IUnaryOperation { OperatorKind: UnaryOperatorKind.Hat } hat)
        {
            TypeId i32 = _builder.Primitive(TypeKind.I32);
            OperandId length = Emit(i32, _f.Len(container), span);
            return Emit(i32, _f.Binop(BinOp.Sub, ArithMode.Default, length, Snapshot(LowerValue(hat.Operand), span)), span);
        }

        return null;
    }

    private OperandId LowerRangeSlice(IOperation access)
    {
        IOperation[] children = access.ChildOperations.ToArray();
        SourceSpan span = SpanOf(access);
        var range = (IRangeOperation)children[1];
        PlaceId container = PlaceOf(children[0]);
        TypeId i32 = _builder.Primitive(TypeKind.I32);
        OperandId start = range.LeftOperand is null
            ? _f.Const(_builder.IntConstant(i32, 0))
            : RangeBound(range.LeftOperand, container, span);
        OperandId end = range.RightOperand is null
            ? Emit(i32, _f.Len(container), span)
            : RangeBound(range.RightOperand, container, span);
        TypeId type = frontend.Types.Map(access.Type!, access.Syntax.GetLocation());
        return Emit(type, _f.Slice(container, start, end), span);
    }

    private OperandId RangeBound(IOperation bound, PlaceId container, SourceSpan span)
    {
        IOperation inner = bound is IConversionOperation { Operand.Type.SpecialType: SpecialType.System_Int32 } conversion ? conversion.Operand : bound;
        return FromEnd(inner, container, span) ?? Snapshot(LowerValue(inner), span);
    }

    private static bool IsEnumeratorMember(INamedTypeSymbol type) =>
        type.Name == "IEnumerator" || type is { Name: "Enumerator", ContainingType: { } outer } && TypeMapper.IsSpanLike(outer);

    private OperandId LowerRangeEnumerator(IInvocationOperation enumeration)
    {
        SourceSpan span = SpanOf(enumeration);
        IOperation source = enumeration.Arguments[0].Value;
        while (source is IConversionOperation or IParenthesizedOperation)
        {
            source = source is IConversionOperation conversion ? conversion.Operand : ((IParenthesizedOperation)source).Operand;
        }

        if (source is not IRangeOperation range || range.LeftOperand is null || range.RightOperand is null)
        {
            throw new UnsupportedConstructException("foreach over a range needs a range with both bounds written", enumeration.Syntax.GetLocation());
        }

        TypeId i32 = _builder.Primitive(TypeKind.I32);
        OperandId start = Snapshot(LowerValue(BoundValue(range.LeftOperand)), span);
        OperandId end = Snapshot(LowerValue(BoundValue(range.RightOperand)), span);
        OperandId before = Emit(i32, _f.Binop(BinOp.Sub, ArithMode.Wrap, start, _f.Const(_builder.IntConstant(i32, 1))), span);
        TypeId type = frontend.Types.Map(enumeration.Type!, enumeration.Syntax.GetLocation());
        return Emit(type, _f.Aggregate(type, [before, end]), span);
    }

    private static IOperation BoundValue(IOperation bound)
    {
        if (bound is IUnaryOperation { OperatorKind: UnaryOperatorKind.Hat })
        {
            throw new UnsupportedConstructException("foreach over a range with a bound from the end is not supported", bound.Syntax.GetLocation());
        }

        return bound is IConversionOperation { Operand.Type.SpecialType: SpecialType.System_Int32 } conversion ? conversion.Operand : bound;
    }

    private OperandId LowerRangeMoveNext(IInvocationOperation advance)
    {
        SourceSpan span = SpanOf(advance);
        TypeId i32 = _builder.Primitive(TypeKind.I32);
        PlaceId enumerator = PlaceOf(advance.Instance!);
        PlaceId current = _f.Project(enumerator, ProjectionKind.Field, RangeCurrent);
        OperandId next = Emit(i32, _f.Binop(BinOp.Add, ArithMode.Wrap, _f.Copy(current), _f.Const(_builder.IntConstant(i32, 1))), span);
        _f.Assign(current, _f.Use(next), span);
        OperandId end = _f.Copy(_f.Project(enumerator, ProjectionKind.Field, RangeEnd));
        return Emit(_builder.Primitive(TypeKind.Bool), _f.Binop(BinOp.Lt, ArithMode.Default, next, end), span);
    }

    private OperandId LowerArrayEnumerator(IInvocationOperation enumeration)
    {
        SourceSpan span = SpanOf(enumeration);
        IOperation source = enumeration.Instance!;
        while (source is IConversionOperation conversion)
        {
            source = conversion.Operand;
        }

        PlaceId array = PlaceOf(source);
        TypeId arrayType = IrTyper.OfPlace(_module, _body, array);
        TypeId i32 = _builder.Primitive(TypeKind.I32);
        TypeId sliceType = _builder.SliceOf(new TypeId(_module.Types[arrayType.Index].A), false);
        OperandId items = Emit(sliceType, _f.Slice(array, _f.Const(_builder.IntConstant(i32, 0)), _f.Const(_builder.IntConstant(i32, _module.Types[arrayType.Index].B))), span);
        TypeId type = _builder.StructType(frontend.Types.EnumeratorOf(sliceType));
        return Emit(type, _f.Aggregate(type, [items, _f.Const(_builder.IntConstant(i32, -1))]), span);
    }

    private OperandId LowerFixedInitializer(IOperation initializer)
    {
        SourceSpan span = SpanOf(initializer);
        IOperation source = initializer.ChildOperations.FirstOrDefault() ?? throw new UnsupportedConstructException("a fixed initializer has no operand", initializer.Syntax.GetLocation());
        while (source is IConversionOperation conversion)
        {
            source = conversion.Operand;
        }

        TypeId pointerType = frontend.Types.Map(initializer.Type is IPointerTypeSymbol ? initializer.Type : throw new UnsupportedConstructException("a fixed initializer is not a pointer", initializer.Syntax.GetLocation()), initializer.Syntax.GetLocation());
        if (source.Type is IArrayTypeSymbol)
        {
            PlaceId array = PlaceOf(source);
            TypeId arrayType = IrTyper.OfPlace(_module, _body, array);
            return AddressOfFirst(array, new TypeId(_module.Types[arrayType.Index].A), pointerType, span);
        }

        return Emit(pointerType, _f.Cast(CastKind.SlicePtr, ArithMode.Default, LowerValue(source), pointerType), span);
    }

    private OperandId LowerCollectionExpression(ICollectionExpressionOperation collection)
    {
        SourceSpan span = SpanOf(collection);
        TypeId type = frontend.Types.Map(collection.Type!, collection.Syntax.GetLocation());
        TypeNode slice = _module.Types[type.Index];
        TypeId arrayType = _builder.ArrayOf(new TypeId(slice.A), collection.Elements.Length);
        LocalId storage = NewTemp(arrayType);
        var values = new List<OperandId>();
        foreach (IOperation element in collection.Elements)
        {
            if (element is ISpreadOperation)
            {
                throw new UnsupportedConstructException("a spread element is not supported yet", element.Syntax.GetLocation());
            }

            values.Add(Snapshot(LowerValue(element), span));
        }

        _f.Assign(_f.LocalPlace(storage), _f.Aggregate(arrayType, values), span);
        TypeId i32 = _builder.Primitive(TypeKind.I32);
        OperandId start = _f.Const(_builder.IntConstant(i32, 0));
        OperandId end = _f.Const(_builder.IntConstant(i32, collection.Elements.Length));
        return Emit(type, _f.Slice(_f.LocalPlace(storage), start, end), span);
    }

    private OperandId LowerGetEnumerator(IInvocationOperation enumeration)
    {
        SourceSpan span = SpanOf(enumeration);
        TypeId type = frontend.Types.Map(enumeration.Type!, enumeration.Syntax.GetLocation());
        OperandId items = Snapshot(LowerValue(enumeration.Instance!), span);
        OperandId before = _f.Const(_builder.IntConstant(_builder.Primitive(TypeKind.I32), -1));
        return Emit(type, _f.Aggregate(type, [items, before]), span);
    }

    private OperandId LowerMoveNext(IInvocationOperation advance)
    {
        SourceSpan span = SpanOf(advance);
        TypeId i32 = _builder.Primitive(TypeKind.I32);
        PlaceId enumerator = PlaceOf(advance.Instance!);
        PlaceId index = _f.Project(enumerator, ProjectionKind.Field, EnumeratorIndex);
        OperandId next = Emit(i32, _f.Binop(BinOp.Add, ArithMode.Wrap, _f.Copy(index), _f.Const(_builder.IntConstant(i32, 1))), span);
        _f.Assign(index, _f.Use(next), span);
        OperandId length = Emit(i32, _f.Len(_f.Project(enumerator, ProjectionKind.Field, EnumeratorItems)), span);
        return Emit(_builder.Primitive(TypeKind.Bool), _f.Binop(BinOp.Lt, ArithMode.Default, next, length), span);
    }

    private OperandId LowerSliceOfWhole(IOperation source, TypeId type, SourceSpan span)
    {
        PlaceId place = PlaceOf(source);
        TypeId i32 = _builder.Primitive(TypeKind.I32);
        OperandId end = Emit(i32, _f.Len(place), span);
        return Emit(type, _f.Slice(place, _f.Const(_builder.IntConstant(i32, 0)), end), span);
    }

    private OperandId LowerStackAlloc(IOperation operation)
    {
        Location location = operation.Syntax.GetLocation();
        SourceSpan span = SpanOf(operation);
        TypeId pointerType = frontend.Types.Map(operation.Type!, location);
        TypeId element = IrTyper.Pointee(_module, pointerType);
        IArrayInitializerOperation? initializer = operation.ChildOperations.OfType<IArrayInitializerOperation>().FirstOrDefault();
        IOperation? size = operation.ChildOperations.FirstOrDefault(child => child is not IArrayInitializerOperation);
        int count = initializer?.ElementValues.Length ?? 0;
        if (size is not null && size.ConstantValue is { HasValue: true, Value: int declared })
        {
            count = declared;
        }
        else if (size is not null)
        {
            TypeId usize = _builder.Primitive(TypeKind.USize);
            OperandId length = Emit(usize, _f.Cast(CastKind.IntInt, ArithMode.Default, LowerValue(size), usize), span);
            return Emit(pointerType, _f.Alloca(element, length, AllocaInit.Zero), span);
        }

        TypeId arrayType = _builder.ArrayOf(element, count);
        LocalId storage = NewTemp(arrayType);
        if (initializer is not null)
        {
            var values = new List<OperandId>();
            foreach (IOperation item in initializer.ElementValues)
            {
                values.Add(Snapshot(LowerValue(item), span));
            }

            _f.Assign(_f.LocalPlace(storage), _f.Aggregate(arrayType, values), span);
        }
        else
        {
            _f.Assign(_f.LocalPlace(storage), _f.Use(Zero(arrayType, span)), span);
        }

        return AddressOfFirst(storage, element, pointerType, span);
    }

    private OperandId AddressOfFirst(LocalId storage, TypeId element, TypeId pointerType, SourceSpan span) =>
        AddressOfFirst(_f.LocalPlace(storage), element, pointerType, span);

    private OperandId AddressOfFirst(PlaceId array, TypeId element, TypeId pointerType, SourceSpan span)
    {
        OperandId zero = _f.Const(_builder.IntConstant(_builder.Primitive(TypeKind.I32), 0));
        PlaceId first = _f.Project(array, ProjectionKind.Index, zero.Index);
        TypeId unique = _builder.Pointer(element, TypeFlags.None);
        OperandId address = Emit(unique, _f.Addr(first, true), span);
        return Emit(pointerType, _f.Cast(CastKind.PtrPtr, ArithMode.Default, address, pointerType), span);
    }

    private OperandId LowerIsNull(IIsNullOperation isNull)
    {
        SourceSpan span = SpanOf(isNull);
        TypeId boolean = _builder.Primitive(TypeKind.Bool);
        if (TypeMapper.IsNullable(isNull.Operand.Type))
        {
            OperandId present = Emit(boolean, _f.HasPayload(PlaceOf(isNull.Operand)), span);
            return Emit(boolean, _f.Unop(UnOp.Not, ArithMode.Default, present), span);
        }

        OperandId value = LowerValue(isNull.Operand);
        return Emit(boolean, _f.Binop(BinOp.Eq, ArithMode.Default, value, _f.Const(_builder.NullConstant(OperandType(value)))), span);
    }

    private OperandId LowerNullableMember(IPropertyReferenceOperation member)
    {
        SourceSpan span = SpanOf(member);
        PlaceId place = PlaceOf(member.Instance!);
        if (member.Property.Name == "HasValue")
        {
            return Emit(_builder.Primitive(TypeKind.Bool), _f.HasPayload(place), span);
        }

        return _f.Copy(_f.Project(place, ProjectionKind.Payload, 0));
    }

    private OperandId LowerGetValueOrDefault(IInvocationOperation invocation, SourceSpan span)
    {
        TypeId payloadType = frontend.Types.Map(invocation.Type!, invocation.Syntax.GetLocation());
        PlaceId place = PlaceOf(invocation.Instance!);
        OperandId fallback = invocation.Arguments.Length == 1
            ? Snapshot(LowerValue(invocation.Arguments[0].Value), span)
            : Zero(payloadType, span);
        LocalId result = NewTemp(payloadType);
        OperandId present = Emit(_builder.Primitive(TypeKind.Bool), _f.HasPayload(place), span);
        BlockId has = _f.NewBlock();
        BlockId missing = _f.NewBlock();
        BlockId join = _f.NewBlock();
        _f.Branch(present, has, missing, span);
        _f.SetBlock(has);
        _f.Assign(_f.LocalPlace(result), _f.Use(_f.Copy(_f.Project(place, ProjectionKind.Payload, 0))), span);
        _f.Goto(join, span);
        _f.SetBlock(missing);
        _f.Assign(_f.LocalPlace(result), _f.Use(fallback), span);
        _f.Goto(join, span);
        _f.SetBlock(join);
        return _f.Copy(_f.LocalPlace(result));
    }

    private OperandId LowerTuple(ITupleOperation tuple)
    {
        SourceSpan span = SpanOf(tuple);
        var elements = new List<OperandId>();
        foreach (IOperation element in tuple.Elements)
        {
            elements.Add(Snapshot(LowerValue(element), span));
        }

        TypeId type = frontend.Types.Map(tuple.Type!, tuple.Syntax.GetLocation());
        return Emit(type, _f.Aggregate(type, elements), span);
    }

    private OperandId LowerDeconstruction(IDeconstructionAssignmentOperation deconstruction)
    {
        SourceSpan span = SpanOf(deconstruction);
        OperandId value = LowerValue(deconstruction.Value);
        LocalId held = NewTemp(OperandType(value));
        _f.Assign(_f.LocalPlace(held), _f.Use(value), span);
        ITupleOperation targets = deconstruction.Target switch
        {
            ITupleOperation tuple => tuple,
            IDeclarationExpressionOperation { Expression: ITupleOperation tuple } => tuple,
            _ => throw new UnsupportedConstructException("this deconstruction is not supported yet", deconstruction.Syntax.GetLocation()),
        };

        int index = 0;
        foreach (IOperation target in targets.Elements)
        {
            OperandId part = _f.Copy(_f.Project(_f.LocalPlace(held), ProjectionKind.Field, index++));
            _f.Assign(PlaceOf(target), _f.Use(part), span);
        }

        return _f.Copy(_f.LocalPlace(held));
    }

    private OperandId TestPattern(IPatternOperation pattern, PlaceId place, SourceSpan span)
    {
        TypeId boolean = _builder.Primitive(TypeKind.Bool);
        if (_module.Types[IrTyper.OfPlace(_module, _body, place).Index].Kind == TypeKind.Optional
            && pattern is not (IDiscardPatternOperation or INegatedPatternOperation or IBinaryPatternOperation))
        {
            if (pattern is IConstantPatternOperation nullTest && IsNullLiteral(nullTest.Value))
            {
                OperandId present = Emit(boolean, _f.HasPayload(place), span);
                return Emit(boolean, _f.Unop(UnOp.Not, ArithMode.Default, present), span);
            }

            return TestPayload(pattern, place, span);
        }

        switch (pattern)
        {
            case IConstantPatternOperation constant:
                return Emit(boolean, _f.Binop(BinOp.Eq, ArithMode.Default, _f.Copy(place), LowerValue(constant.Value)), span);
            case IRelationalPatternOperation relational:
                BinOp op = relational.OperatorKind switch
                {
                    BinaryOperatorKind.LessThan => BinOp.Lt,
                    BinaryOperatorKind.LessThanOrEqual => BinOp.Le,
                    BinaryOperatorKind.GreaterThan => BinOp.Gt,
                    BinaryOperatorKind.GreaterThanOrEqual => BinOp.Ge,
                    _ => throw new UnsupportedConstructException("the relational pattern is not supported yet", pattern.Syntax.GetLocation()),
                };
                return Emit(boolean, _f.Binop(op, ArithMode.Default, _f.Copy(place), LowerValue(relational.Value)), span);
            case IBinaryPatternOperation binary:
                OperandId left = TestPattern(binary.LeftPattern, place, span);
                OperandId right = TestPattern(binary.RightPattern, place, span);
                BinOp combine = binary.OperatorKind == BinaryOperatorKind.And ? BinOp.And : BinOp.Or;
                return Emit(boolean, _f.Binop(combine, ArithMode.Default, left, right), span);
            case INegatedPatternOperation negated:
                return Emit(boolean, _f.Unop(UnOp.Not, ArithMode.Default, TestPattern(negated.Pattern, place, span)), span);
            case IDiscardPatternOperation:
                return _f.Const(_builder.BoolConstant(boolean, true));
            case IDeclarationPatternOperation declaration:
                if (declaration.DeclaredSymbol is ILocalSymbol declared)
                {
                    _f.Assign(_f.LocalPlace(LocalOf(declared, span)), _f.Use(_f.Copy(place)), span);
                }

                return _f.Const(_builder.BoolConstant(boolean, true));
            case IRecursivePatternOperation recursive:
                return TestRecursive(recursive, place, span);
            default:
                throw new UnsupportedConstructException("the pattern " + pattern.Kind + " is not supported yet", pattern.Syntax.GetLocation());
        }
    }

    private OperandId TestPayload(IPatternOperation pattern, PlaceId place, SourceSpan span)
    {
        TypeId boolean = _builder.Primitive(TypeKind.Bool);
        LocalId result = NewTemp(boolean);
        _f.Assign(_f.LocalPlace(result), _f.Use(_f.Const(_builder.BoolConstant(boolean, false))), span);
        foreach (IOperation node in pattern.DescendantsAndSelf())
        {
            ILocalSymbol? bound = node switch
            {
                IDeclarationPatternOperation declaration => declaration.DeclaredSymbol as ILocalSymbol,
                IRecursivePatternOperation recursive => recursive.DeclaredSymbol as ILocalSymbol,
                _ => null,
            };
            if (bound is not null)
            {
                LocalId local = LocalOf(bound, span);
                _f.Assign(_f.LocalPlace(local), _f.Use(Zero(_body.Locals[local.Index].Type, span)), span);
            }
        }

        OperandId present = Emit(boolean, _f.HasPayload(place), span);
        BlockId inner = _f.NewBlock();
        BlockId join = _f.NewBlock();
        _f.Branch(present, inner, join, span);
        _f.SetBlock(inner);
        OperandId matched = TestPattern(pattern, _f.Project(place, ProjectionKind.Payload, 0), span);
        _f.Assign(_f.LocalPlace(result), _f.Use(matched), span);
        _f.Goto(join, span);
        _f.SetBlock(join);
        return _f.Copy(_f.LocalPlace(result));
    }

    private OperandId TestRecursive(IRecursivePatternOperation pattern, PlaceId place, SourceSpan span)
    {
        TypeId boolean = _builder.Primitive(TypeKind.Bool);
        OperandId result = _f.Const(_builder.BoolConstant(boolean, true));
        TypeId inputType = IrTyper.OfPlace(_module, _body, place);
        if (pattern.DeclaredSymbol is ILocalSymbol declared)
        {
            _f.Assign(_f.LocalPlace(LocalOf(declared, span)), _f.Use(_f.Copy(place)), span);
        }

        foreach (IPropertySubpatternOperation sub in pattern.PropertySubpatterns)
        {
            if (sub.Member is not IFieldReferenceOperation member)
            {
                throw new UnsupportedConstructException("a property pattern names something that is not a field", sub.Syntax.GetLocation());
            }

            int index = frontend.Types.FieldIndex((INamedTypeSymbol)member.Field.ContainingType, member.Field);
            OperandId test = TestPattern(sub.Pattern, _f.Project(place, ProjectionKind.Field, index), span);
            result = Emit(boolean, _f.Binop(BinOp.And, ArithMode.Default, result, test), span);
        }

        int position = 0;
        foreach (IPatternOperation sub in pattern.DeconstructionSubpatterns)
        {
            OperandId test = TestPattern(sub, _f.Project(place, ProjectionKind.Field, position++), span);
            result = Emit(boolean, _f.Binop(BinOp.And, ArithMode.Default, result, test), span);
        }

        _ = inputType;
        return result;
    }

    private OperandId ReferenceTo(IOperation operation, bool readOnly, SourceSpan span)
    {
        PlaceId place = PlaceOf(operation);
        TypeId pointee = IrTyper.OfPlace(_module, _body, place);
        return Emit(_builder.Pointer(pointee, readOnly ? TypeFlags.Const : TypeFlags.None), _f.Addr(place, !readOnly), span);
    }

    private OperandId LowerAddressOf(IAddressOfOperation address)
    {
        SourceSpan span = SpanOf(address);
        PlaceId place = PlaceOf(address.Reference);
        TypeId pointee = IrTyper.OfPlace(_module, _body, place);
        OperandId pointer = Emit(_builder.Pointer(pointee, TypeFlags.None), _f.Addr(place, true), span);
        TypeId target = frontend.Types.Map(address.Type!, address.Syntax.GetLocation());
        return Emit(target, _f.Cast(CastKind.PtrPtr, ArithMode.Default, pointer, target), span);
    }

    private OperandId LowerRefAssignment(ISimpleAssignmentOperation assignment)
    {
        SourceSpan span = SpanOf(assignment);
        LocalId target = assignment.Target switch
        {
            ILocalReferenceOperation local => LocalOf(local.Local, span),
            IParameterReferenceOperation parameter => _locals[parameter.Parameter],
            _ => throw new UnsupportedConstructException("a ref assignment target is not supported yet", assignment.Syntax.GetLocation()),
        };
        PlaceId source = PlaceOf(assignment.Value);
        bool mutable = assignment.Target is ILocalReferenceOperation { Local.RefKind: RefKind.Ref } or IParameterReferenceOperation { Parameter.RefKind: RefKind.Ref };
        _f.Assign(_f.LocalPlace(target), _f.Addr(source, mutable), span);
        return _f.Copy(_f.LocalPlace(target));
    }

    private OperandId Zero(TypeId type, SourceSpan span) => _f.Const(ZeroConstant(type));

    private ConstId ZeroConstant(TypeId type)
    {
        TypeNode node = _module.Types[type.Index];
        switch (node.Kind)
        {
            case TypeKind.Bool:
                return _builder.BoolConstant(type, false);
            case TypeKind.Ptr:
                return _builder.NullConstant(type);
            case TypeKind.F32 or TypeKind.F64 or TypeKind.F16:
                return _builder.FloatConstant(type, 0);
            case TypeKind.Slice:
                TypeId elementPointer = _builder.Pointer(new TypeId(node.A), TypeFlags.Nullable | TypeFlags.Many | (node.Flags & TypeFlags.Const));
                return _builder.AggregateConstant(type, [_builder.NullConstant(elementPointer), _builder.IntConstant(_builder.Primitive(TypeKind.I32), 0)]);
            case TypeKind.Optional:
                return _builder.AggregateConstant(type, [ZeroConstant(new TypeId(node.A)), _builder.BoolConstant(_builder.Primitive(TypeKind.Bool), false)]);
            case TypeKind.Array:
                var elements = new List<ConstId>();
                for (int i = 0; i < node.B; i++)
                {
                    elements.Add(ZeroConstant(new TypeId(node.A)));
                }

                return _builder.AggregateConstant(type, elements);
            case TypeKind.Struct:
                StructDecl declaration = _module.Structs[node.A];
                var fields = new List<ConstId>();
                for (int i = 0; i < declaration.Fields.Count; i++)
                {
                    fields.Add(ZeroConstant(_module.Fields[declaration.Fields.Start + i].Type));
                }

                return _builder.AggregateConstant(type, fields);
            default:
                return _builder.IntConstant(type, 0);
        }
    }
}
