namespace CDelta.Ir;

public sealed class FunctionBuilder(Module module, Body body)
{
    private BlockId _current = BlockId.None;
    private List<StatementId>? _sink;

    public Body Body { get; } = body;

    public BlockId Current => _current;

    public LocalId NewLocal(string? name, TypeId type, LocalKind kind, SourceSpan span)
    {
        StringId nameId = name is null ? StringId.None : module.Intern(name);
        Body.Locals.Add(new LocalDecl(nameId, type, kind, span));
        return new LocalId(Body.Locals.Count - 1);
    }

    public BlockId NewBlock()
    {
        Body.Blocks.Add(new Block([], null));
        return new BlockId(Body.Blocks.Count - 1);
    }

    public void SetBlock(BlockId block) => _current = block;

    public void RedirectStatements(List<StatementId>? sink) => _sink = sink;

    public bool IsTerminated(BlockId block) => Body.Blocks[block.Index].Terminator is not null;

    public PlaceId LocalPlace(LocalId local) => AddPlace(new Place(PlaceBase.Local, local.Index, IdRange.Empty));

    public OperandId Copy(PlaceId place) => AddOperand(new Operand(OperandKind.Copy, place.Index));

    public OperandId Const(ConstId constant) => AddOperand(new Operand(OperandKind.Const, constant.Index));

    public OperandId Undef(TypeId type) => AddOperand(new Operand(OperandKind.Undef, type.Index));

    public RvalueId Use(OperandId operand) => AddRvalue(Blank(RvalueKind.Use) with { A = operand });

    public RvalueId Binop(BinOp op, ArithMode mode, OperandId left, OperandId right) =>
        AddRvalue(Blank(RvalueKind.Binop) with { Op = (int)op, Mode = mode, A = left, B = right });

    public RvalueId Unop(UnOp op, ArithMode mode, OperandId operand) =>
        AddRvalue(Blank(RvalueKind.Unop) with { Op = (int)op, Mode = mode, A = operand });

    public RvalueId Cast(CastKind kind, ArithMode mode, OperandId operand, TypeId target) =>
        AddRvalue(Blank(RvalueKind.Cast) with { Op = (int)kind, Mode = mode, A = operand, Type = target });

    public RvalueId CallDirect(FunctionId callee, IReadOnlyList<OperandId> args) =>
        AddRvalue(Blank(RvalueKind.Call) with { Callee = CalleeKind.Direct, CalleeId = callee.Index, Args = AddArgs(args) });

    public RvalueId CallIndirect(OperandId callee, IReadOnlyList<OperandId> args) =>
        AddRvalue(Blank(RvalueKind.Call) with { Callee = CalleeKind.Indirect, CalleeId = callee.Index, Args = AddArgs(args) });

    public RvalueId Addr(PlaceId place, bool mutable) =>
        AddRvalue(Blank(RvalueKind.Addr) with { Place = place, Aux = mutable ? 1 : 0 });

    public RvalueId Aggregate(TypeId type, IReadOnlyList<OperandId> elements) =>
        AddRvalue(Blank(RvalueKind.Aggregate) with { Type = type, Args = AddArgs(elements) });

    public RvalueId Len(PlaceId place) => AddRvalue(Blank(RvalueKind.Len) with { Place = place });

    public RvalueId Slice(PlaceId place, OperandId start, OperandId end) =>
        AddRvalue(Blank(RvalueKind.Slice) with { Place = place, A = start, B = end });

    public RvalueId Alloca(TypeId element, OperandId count, AllocaInit init) =>
        AddRvalue(Blank(RvalueKind.Alloca) with { Type = element, A = count, Aux = (int)init });

    public RvalueId HasPayload(PlaceId place) => AddRvalue(Blank(RvalueKind.HasPayload) with { Place = place });

    public RvalueId OptionalSome(OperandId value) => AddRvalue(Blank(RvalueKind.OptionalSome) with { A = value });

    public RvalueId OptionalNone(TypeId payload) => AddRvalue(Blank(RvalueKind.OptionalNone) with { Type = payload });

    public RvalueId ResultOk(TypeId type, OperandId value) => AddRvalue(Blank(RvalueKind.ResultOk) with { Type = type, A = value });

    public RvalueId ResultErr(TypeId type, OperandId error) => AddRvalue(Blank(RvalueKind.ResultErr) with { Type = type, A = error });

    public RvalueId Err(PlaceId place) => AddRvalue(Blank(RvalueKind.Err) with { Place = place });

    public RvalueId Errcode(OperandId error) => AddRvalue(Blank(RvalueKind.Errcode) with { A = error });

    public RvalueId Intrinsic(string name, IReadOnlyList<OperandId> args) =>
        AddRvalue(Blank(RvalueKind.Intrinsic) with { Aux = module.Intern(name).Index, Args = AddArgs(args) });

    public RvalueId SizeOf(TypeId type) => AddRvalue(Blank(RvalueKind.SizeOf) with { Type = type });

    public RvalueId AlignOf(TypeId type) => AddRvalue(Blank(RvalueKind.AlignOf) with { Type = type });

    public RvalueId OffsetOf(TypeId type, int field) => AddRvalue(Blank(RvalueKind.OffsetOf) with { Type = type, Aux = field });

    public PlaceId GlobalPlace(GlobalId global) => AddPlace(new Place(PlaceBase.Global, global.Index, IdRange.Empty));

    public PlaceId DerefPlace(LocalId pointer) => AddPlace(new Place(PlaceBase.DerefLocal, pointer.Index, IdRange.Empty));

    public PlaceId Project(PlaceId place, ProjectionKind kind, int value)
    {
        Place source = Body.Places[place.Index];
        var projections = new List<Projection>();
        for (int i = 0; i < source.Projections.Count; i++)
        {
            projections.Add(Body.Projections[source.Projections.Start + i]);
        }

        projections.Add(new Projection(kind, value));
        int start = Body.Projections.Count;
        Body.Projections.AddRange(projections);
        return AddPlace(source with { Projections = new IdRange(start, projections.Count) });
    }

    public void Assign(PlaceId place, RvalueId rvalue, SourceSpan span) =>
        Emit(new Statement(StatementKind.Assign, place, rvalue, OperandId.None, 0, StringId.None, span));

    public void CallStatement(RvalueId call, SourceSpan span) =>
        Emit(new Statement(StatementKind.Call, PlaceId.None, call, OperandId.None, 0, StringId.None, span));

    public void Check(CheckKind kind, OperandId condition, string message, SourceSpan span) =>
        Emit(new Statement(StatementKind.Check, PlaceId.None, RvalueId.None, condition, (int)kind, module.Intern(message), span));

    public void Live(LocalId local, SourceSpan span) =>
        Emit(new Statement(StatementKind.Live, PlaceId.None, RvalueId.None, OperandId.None, local.Index, StringId.None, span));

    public void Dead(LocalId local, SourceSpan span) =>
        Emit(new Statement(StatementKind.Dead, PlaceId.None, RvalueId.None, OperandId.None, local.Index, StringId.None, span));

    public void TracePush(SourceSpan span) =>
        Emit(new Statement(StatementKind.TracePush, PlaceId.None, RvalueId.None, OperandId.None, 0, StringId.None, span));

    public void TraceSave(LocalId local, SourceSpan span) =>
        Emit(new Statement(StatementKind.TraceSave, PlaceId.None, RvalueId.None, OperandId.None, local.Index, StringId.None, span));

    public void TraceRestore(LocalId local, SourceSpan span) =>
        Emit(new Statement(StatementKind.TraceRestore, PlaceId.None, RvalueId.None, OperandId.None, local.Index, StringId.None, span));

    public void Goto(BlockId target, SourceSpan span) =>
        Terminate(new Terminator(TermKind.Goto, OperandId.None, target, BlockId.None, IdRange.Empty, UnreachableMode.Default, span));

    public void Branch(OperandId condition, BlockId then, BlockId otherwise, SourceSpan span) =>
        Terminate(new Terminator(TermKind.Branch, condition, then, otherwise, IdRange.Empty, UnreachableMode.Default, span));

    public void Switch(OperandId value, IReadOnlyList<SwitchCase> cases, BlockId otherwise, SourceSpan span)
    {
        int start = Body.SwitchCases.Count;
        Body.SwitchCases.AddRange(cases);
        Terminate(new Terminator(TermKind.Switch, value, BlockId.None, otherwise, new IdRange(start, cases.Count), UnreachableMode.Default, span));
    }

    public void Return(OperandId value, SourceSpan span) =>
        Terminate(new Terminator(TermKind.Return, value, BlockId.None, BlockId.None, IdRange.Empty, UnreachableMode.Default, span));

    public void Unreachable(UnreachableMode mode, SourceSpan span) =>
        Terminate(new Terminator(TermKind.Unreachable, OperandId.None, BlockId.None, BlockId.None, IdRange.Empty, mode, span));

    public void Panic(OperandId message, SourceSpan span) =>
        Terminate(new Terminator(TermKind.Panic, message, BlockId.None, BlockId.None, IdRange.Empty, UnreachableMode.Default, span));

    private static Rvalue Blank(RvalueKind kind) =>
        new(kind, 0, ArithMode.Default, OperandId.None, OperandId.None, TypeId.None, CalleeKind.Direct, -1, IdRange.Empty, PlaceId.None, 0);

    private IdRange AddArgs(IReadOnlyList<OperandId> args)
    {
        int start = Body.Args.Count;
        Body.Args.AddRange(args);
        return new IdRange(start, args.Count);
    }

    private PlaceId AddPlace(Place place)
    {
        Body.Places.Add(place);
        return new PlaceId(Body.Places.Count - 1);
    }

    private OperandId AddOperand(Operand operand)
    {
        Body.Operands.Add(operand);
        return new OperandId(Body.Operands.Count - 1);
    }

    private RvalueId AddRvalue(Rvalue rvalue)
    {
        Body.Rvalues.Add(rvalue);
        return new RvalueId(Body.Rvalues.Count - 1);
    }

    private void Emit(Statement statement)
    {
        Body.Statements.Add(statement);
        (_sink ?? Body.Blocks[_current.Index].Statements).Add(new StatementId(Body.Statements.Count - 1));
    }

    private void Terminate(Terminator terminator)
    {
        Block block = Body.Blocks[_current.Index];
        Body.Blocks[_current.Index] = block with { Terminator = terminator };
    }
}
