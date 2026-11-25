using CDelta.Ir;

namespace CDelta.Core;

internal sealed class FunctionSafety(Module module, Body body, Target target, bool checksOn)
{
    private const string DivisionByZero = "division by zero";
    private const string IntegerOverflow = "integer overflow";
    private const string UnwrapError = "attempt to unwrap error";
    private const string ReadErrorOfSuccess = "attempt to read error of success";

    private readonly ModuleBuilder _builder = new(module);
    private readonly FunctionBuilder _f = new(module, body);

    public void Run()
    {
        ArithMode resolved = checksOn ? ArithMode.Trap : ArithMode.Assume;
        for (int i = 0; i < body.Rvalues.Count; i++)
        {
            Rvalue rvalue = body.Rvalues[i];
            if (rvalue.Mode != ArithMode.Default)
            {
                continue;
            }

            body.Rvalues[i] = rvalue with { Mode = resolved };
        }

        if (checksOn)
        {
            for (int i = 0; i < body.Rvalues.Count; i++)
            {
                Rvalue rvalue = body.Rvalues[i];
                if (rvalue.Kind == RvalueKind.Alloca && (AllocaInit)rvalue.Aux == AllocaInit.Undef)
                {
                    body.Rvalues[i] = rvalue with { Aux = (int)AllocaInit.Fill };
                }
            }
        }

        for (int b = 0; b < body.Blocks.Count; b++)
        {
            Block block = body.Blocks[b];
            var rewritten = new List<StatementId>();
            foreach (StatementId id in block.Statements)
            {
                Statement statement = body.Statements[id.Index];
                if (statement.Kind == StatementKind.Assign)
                {
                    InsertDivisionChecks(statement, rewritten);
                }

                if (checksOn)
                {
                    InsertPlaceChecks(statement, rewritten);
                }

                rewritten.Add(id);
                if (statement.Kind == StatementKind.Assign)
                {
                    InsertEnumCheck(statement, rewritten);
                }
            }

            if (checksOn && block.Terminator is Terminator ending && !ending.Operand.IsNone)
            {
                InsertPlaceChecks(
                    new Statement(StatementKind.Check, PlaceId.None, RvalueId.None, ending.Operand, 0, StringId.None, ending.Span),
                    rewritten);
            }

            block.Statements.Clear();
            block.Statements.AddRange(rewritten);
            if (block.Terminator is Terminator { Kind: TermKind.Unreachable, Mode: UnreachableMode.Default } terminator)
            {
                body.Blocks[b] = block with
                {
                    Terminator = terminator with { Mode = checksOn ? UnreachableMode.Trap : UnreachableMode.Assume },
                };
            }
        }
    }

    private void InsertEnumCheck(Statement statement, List<StatementId> sink)
    {
        Rvalue rvalue = body.Rvalues[statement.Rvalue.Index];
        if (rvalue.Kind != RvalueKind.Cast || (CastKind)rvalue.Op != CastKind.IntEnum || rvalue.Mode != ArithMode.Trap)
        {
            return;
        }

        EnumDecl declaration = module.Enums[module.Types[rvalue.Type.Index].A];
        if (!declaration.Exhaustive)
        {
            return;
        }

        TypeId boolean = _builder.Primitive(TypeKind.Bool);
        _f.RedirectStatements(sink);
        try
        {
            OperandId value = Emit(declaration.Underlying, _f.Cast(CastKind.EnumInt, ArithMode.Wrap, _f.Copy(statement.Place), declaration.Underlying), statement.Span);
            OperandId valid = _f.Const(_builder.BoolConstant(boolean, false));
            for (int i = 0; i < declaration.Members.Count; i++)
            {
                EnumMember member = module.EnumMembers[declaration.Members.Start + i];
                OperandId matches = Emit(boolean, _f.Binop(BinOp.Eq, ArithMode.Default, value, _f.Const(_builder.IntConstant(declaration.Underlying, member.Value))), statement.Span);
                valid = Emit(boolean, _f.Binop(BinOp.Or, ArithMode.Default, valid, matches), statement.Span);
            }

            _f.Check(CheckKind.Enum, valid, "invalid enum value", statement.Span);
        }
        finally
        {
            _f.RedirectStatements(null);
        }
    }

    private void InsertPlaceChecks(Statement statement, List<StatementId> sink)
    {
        var places = new List<PlaceId>();
        var addressed = new List<PlaceId>();
        PlaceId destination = statement.Place;
        if (!statement.Place.IsNone)
        {
            places.Add(statement.Place);
        }

        if (!statement.Rvalue.IsNone)
        {
            Rvalue rvalue = body.Rvalues[statement.Rvalue.Index];
            CollectRvaluePlaces(rvalue, places);
            if (rvalue.Kind == RvalueKind.Addr)
            {
                addressed.Add(rvalue.Place);
            }
        }

        if (!statement.Operand.IsNone)
        {
            CollectOperandPlace(statement.Operand, places);
        }

        _f.RedirectStatements(sink);
        try
        {
            foreach (PlaceId place in places)
            {
                CheckPlace(place, statement.Span, true, place != destination);
            }

            if (!statement.Rvalue.IsNone)
            {
                CheckRvalue(body.Rvalues[statement.Rvalue.Index], statement.Span);
            }

            foreach (PlaceId place in addressed)
            {
                CheckPlace(place, statement.Span, false, false);
            }
        }
        finally
        {
            _f.RedirectStatements(null);
        }
    }

    private void CollectOperandPlace(OperandId operand, List<PlaceId> places)
    {
        Operand o = body.Operands[operand.Index];
        if (o.Kind == OperandKind.Copy)
        {
            places.Add(new PlaceId(o.A));
        }
    }

    private void CollectRvaluePlaces(Rvalue rvalue, List<PlaceId> places)
    {
        if (!rvalue.A.IsNone)
        {
            CollectOperandPlace(rvalue.A, places);
        }

        if (!rvalue.B.IsNone)
        {
            CollectOperandPlace(rvalue.B, places);
        }

        for (int i = 0; i < rvalue.Args.Count && rvalue.Kind is RvalueKind.Call or RvalueKind.Aggregate; i++)
        {
            CollectOperandPlace(body.Args[rvalue.Args.Start + i], places);
        }

        if (!rvalue.Place.IsNone && rvalue.Kind != RvalueKind.Addr)
        {
            places.Add(rvalue.Place);
        }
    }

    private void CheckPlace(PlaceId id, SourceSpan span, bool checkNull, bool isRead)
    {
        Place place = body.Places[id.Index];
        TypeId boolean = _builder.Primitive(TypeKind.Bool);
        TypeId current;
        if (place.Base == PlaceBase.DerefLocal)
        {
            TypeId pointer = body.Locals[place.Id].Type;
            TypeNode node = module.Types[pointer.Index];
            if (checkNull && node.Flags.HasFlag(TypeFlags.Nullable))
            {
                OperandId pointerValue = _f.Copy(_f.LocalPlace(new LocalId(place.Id)));
                OperandId isSet = Emit(boolean, _f.Binop(BinOp.Ne, ArithMode.Default, pointerValue, _f.Const(_builder.NullConstant(pointer))), span);
                _f.Check(CheckKind.Null, isSet, "null pointer dereference", span);
            }

            current = IrTyper.Pointee(module, pointer);
        }
        else
        {
            current = place.Base == PlaceBase.Local ? body.Locals[place.Id].Type : module.Globals[place.Id].Type;
        }

        for (int i = 0; i < place.Projections.Count; i++)
        {
            Projection projection = body.Projections[place.Projections.Start + i];
            if (projection.Kind == ProjectionKind.Index)
            {
                CheckIndex(place, i, current, projection, span);
            }

            if (projection.Kind == ProjectionKind.Payload && isRead)
            {
                OperandId present = Emit(boolean, _f.HasPayload(PrefixPlace(place, i)), span);
                if (module.Types[current.Index].Kind == TypeKind.Result)
                {
                    _f.Check(CheckKind.ResultValue, present, UnwrapError, span);
                }
                else
                {
                    _f.Check(CheckKind.Optional, present, "optional has no value", span);
                }
            }

            current = IrTyper.Project(module, current, projection);
        }
    }

    private PlaceId PrefixPlace(Place place, int depth)
    {
        body.Places.Add(place with { Projections = new IdRange(place.Projections.Start, depth) });
        return new PlaceId(body.Places.Count - 1);
    }

    private void CheckRvalue(Rvalue rvalue, SourceSpan span)
    {
        TypeId boolean = _builder.Primitive(TypeKind.Bool);
        TypeId usize = _builder.Primitive(TypeKind.USize);
        TypeId i32 = _builder.Primitive(TypeKind.I32);
        if (rvalue.Kind == RvalueKind.Slice)
        {
            TypeId container = IrTyper.OfPlace(module, body, rvalue.Place);
            TypeNode node = module.Types[container.Index];
            OperandId length = node.Kind == TypeKind.Array
                ? _f.Const(_builder.IntConstant(i32, node.B))
                : Emit(i32, _f.Len(rvalue.Place), span);
            OperandId start = Emit(usize, _f.Cast(CastKind.IntInt, ArithMode.Wrap, rvalue.A, usize), span);
            OperandId end = Emit(usize, _f.Cast(CastKind.IntInt, ArithMode.Wrap, rvalue.B, usize), span);
            OperandId limit = Emit(usize, _f.Cast(CastKind.IntInt, ArithMode.Wrap, length, usize), span);
            OperandId ordered = Emit(boolean, _f.Binop(BinOp.Le, ArithMode.Default, start, end), span);
            OperandId inside = Emit(boolean, _f.Binop(BinOp.Le, ArithMode.Default, end, limit), span);
            OperandId valid = Emit(boolean, _f.Binop(BinOp.And, ArithMode.Default, ordered, inside), span);
            _f.Check(CheckKind.Bounds, valid, "slice out of bounds", span);
        }
        else if (rvalue.Kind == RvalueKind.Err)
        {
            OperandId succeeded = Emit(boolean, _f.HasPayload(rvalue.Place), span);
            OperandId failed = Emit(boolean, _f.Unop(UnOp.Not, ArithMode.Default, succeeded), span);
            _f.Check(CheckKind.ResultError, failed, ReadErrorOfSuccess, span);
        }
        else if (rvalue.Kind == RvalueKind.Aggregate && module.Types[rvalue.Type.Index].Kind == TypeKind.Slice)
        {
            OperandId count = body.Args[rvalue.Args.Start + 1];
            OperandId nonNegative = Emit(boolean, _f.Binop(BinOp.Ge, ArithMode.Default, count, _f.Const(_builder.IntConstant(i32, 0))), span);
            _f.Check(CheckKind.Bounds, nonNegative, "negative slice length", span);
        }
    }

    private void CheckIndex(Place place, int depth, TypeId container, Projection projection, SourceSpan span)
    {
        TypeNode node = module.Types[container.Index];
        TypeId boolean = _builder.Primitive(TypeKind.Bool);
        TypeId usize = _builder.Primitive(TypeKind.USize);
        OperandId index = new(projection.Value);
        OperandId asUnsigned = Emit(usize, _f.Cast(CastKind.IntInt, ArithMode.Wrap, index, usize), span);
        OperandId limit;
        if (node.Kind == TypeKind.Array)
        {
            limit = _f.Const(_builder.IntConstant(usize, node.B));
        }
        else
        {
            OperandId length = Emit(_builder.Primitive(TypeKind.I32), _f.Len(PrefixPlace(place, depth)), span);
            limit = Emit(usize, _f.Cast(CastKind.IntInt, ArithMode.Wrap, length, usize), span);
        }

        OperandId inRange = Emit(boolean, _f.Binop(BinOp.Lt, ArithMode.Default, asUnsigned, limit), span);
        _f.Check(CheckKind.Bounds, inRange, "index out of bounds", span);
    }

    private void InsertDivisionChecks(Statement statement, List<StatementId> sink)
    {
        Rvalue rvalue = body.Rvalues[statement.Rvalue.Index];
        if (rvalue.Kind != RvalueKind.Binop || (BinOp)rvalue.Op is not (BinOp.Div or BinOp.Rem))
        {
            return;
        }

        TypeId type = IrTyper.OfOperand(module, body, rvalue.A);
        TypeKind kind = module.Types[type.Index].Kind;
        TypeId boolean = _builder.Primitive(TypeKind.Bool);
        _f.RedirectStatements(sink);
        try
        {
            OperandId zero = _f.Const(_builder.IntConstant(type, 0));
            if (!IsNonZeroConstant(rvalue.B))
            {
                OperandId nonZero = Emit(boolean, _f.Binop(BinOp.Ne, ArithMode.Default, rvalue.B, zero), statement.Span);
                _f.Check(CheckKind.DivZero, nonZero, DivisionByZero, statement.Span);
            }

            if (IrTypes.IsSigned(kind, target) && !IsConstantOtherThanMinusOne(rvalue.B))
            {
                int width = IrTypes.BitWidth(kind, target);
                Int128 min = -(Int128.One << (width - 1));
                OperandId isMin = Emit(boolean, _f.Binop(BinOp.Eq, ArithMode.Default, rvalue.A, _f.Const(_builder.IntConstant(type, min))), statement.Span);
                OperandId isMinusOne = Emit(boolean, _f.Binop(BinOp.Eq, ArithMode.Default, rvalue.B, _f.Const(_builder.IntConstant(type, -1))), statement.Span);
                OperandId both = Emit(boolean, _f.Binop(BinOp.And, ArithMode.Default, isMin, isMinusOne), statement.Span);
                OperandId safe = Emit(boolean, _f.Unop(UnOp.Not, ArithMode.Default, both), statement.Span);
                _f.Check(CheckKind.DivZero, safe, IntegerOverflow, statement.Span);
            }
        }
        finally
        {
            _f.RedirectStatements(null);
        }
    }

    private OperandId Emit(TypeId type, RvalueId rvalue, SourceSpan span)
    {
        LocalId temp = _f.NewLocal(null, type, LocalKind.Temp, SourceSpan.None);
        _f.Assign(_f.LocalPlace(temp), rvalue, span);
        return _f.Copy(_f.LocalPlace(temp));
    }

    private bool IsNonZeroConstant(OperandId operand)
    {
        Operand o = body.Operands[operand.Index];
        return o.Kind == OperandKind.Const && module.Constants[o.A].Kind == ConstKind.Int && module.Constants[o.A].Value != 0;
    }

    private bool IsConstantOtherThanMinusOne(OperandId operand)
    {
        Operand o = body.Operands[operand.Index];
        return o.Kind == OperandKind.Const && module.Constants[o.A].Kind == ConstKind.Int && module.Constants[o.A].Value != -1;
    }
}
