using CDelta.Diagnostics;

namespace CDelta.Ir;

internal sealed class FunctionVerifier(Module module, ModuleStage stage, FunctionId id, DiagnosticBag bag)
{
    private readonly FunctionDecl _function = module.Functions[id.Index];
    private readonly Body? _body = module.Bodies[id.Index];
    private readonly string _name = module.Text(module.Functions[id.Index].Key);
    private SourceSpan _span;

    public void Run()
    {
        _span = _function.Span;
        VerifyDeclaration();
        if (_body is null)
        {
            if (_function.Linkage == LinkageKind.Internal)
            {
                Fail("an internal function has no body");
            }

            return;
        }

        VerifyBody(_body);
    }

    private void Fail(string detail) => IrVerifier.Report(bag, module, _span, _name, detail);

    private bool ValidType(TypeId type) => type.Index >= 0 && type.Index < module.Types.Count;

    private TypeKind KindOf(TypeId type) => module.Types[type.Index].Kind;

    private void VerifyDeclaration()
    {
        if (!ValidType(_function.Return))
        {
            Fail("return type does not resolve");
        }

        for (int i = 0; i < _function.Params.Count; i++)
        {
            if (!ValidType(module.Params[_function.Params.Start + i].Type))
            {
                Fail("parameter type does not resolve");
            }
        }

        if (stage == ModuleStage.Final && _function.Linkage is LinkageKind.Extern or LinkageKind.CMacro
            && _function.Origin.Kind == OriginKind.Source)
        {
            Fail("an extern declaration has no C origin");
        }
    }

    private void VerifyBody(Body body)
    {
        if (body.Blocks.Count == 0)
        {
            Fail("the body has no blocks");
            return;
        }

        var paramLocals = new List<int>();
        for (int i = 0; i < body.Locals.Count; i++)
        {
            if (!ValidType(body.Locals[i].Type))
            {
                Fail("local %" + i + " type does not resolve");
            }

            if (body.Locals[i].Kind == LocalKind.Param)
            {
                paramLocals.Add(i);
            }
        }

        if (paramLocals.Count != _function.Params.Count)
        {
            Fail("the number of param locals differs from the parameters");
        }

        for (int b = 0; b < body.Blocks.Count; b++)
        {
            VerifyBlock(body, b);
        }

        VerifyReachabilityAndAssignment(body);
    }

    private void VerifyBlock(Body body, int blockIndex)
    {
        Block block = body.Blocks[blockIndex];
        foreach (StatementId statementId in block.Statements)
        {
            Statement statement = body.Statements[statementId.Index];
            _span = statement.Span;
            CheckSpan(statement.Span);
            VerifyStatement(body, statement);
        }

        if (block.Terminator is not Terminator terminator)
        {
            _span = _function.Span;
            Fail("bb" + blockIndex + " has no terminator");
            return;
        }

        _span = terminator.Span;
        CheckSpan(terminator.Span);
        VerifyTerminator(body, terminator);
    }

    private void CheckSpan(SourceSpan span)
    {
        if (_function.Origin.Kind != OriginKind.Synthetic && (span.IsNone || span.File.Index >= module.Sources.Count))
        {
            Fail("a statement has no span");
        }
    }

    private void VerifyStatement(Body body, Statement statement)
    {
        switch (statement.Kind)
        {
            case StatementKind.Assign:
                TypeId destination = PlaceType(body, statement.Place);
                TypeId value = RvalueType(body, statement.Rvalue);
                if (destination.IsNone || value.IsNone || (destination != value && !ConstSliceOf(body, destination, statement.Rvalue)))
                {
                    Fail("assignment of mismatched types");
                }

                break;
            case StatementKind.Call:
                if (body.Rvalues[statement.Rvalue.Index].Kind != RvalueKind.Call)
                {
                    Fail("a call statement holds something else");
                }

                RvalueType(body, statement.Rvalue);
                break;
            case StatementKind.Check:
                if (stage != ModuleStage.Final)
                {
                    Fail("check is only allowed in the final stage");
                }

                RequireBool(body, statement.Operand);
                break;
            case StatementKind.Intrinsic:
                if (body.Rvalues[statement.Rvalue.Index].Kind != RvalueKind.Intrinsic)
                {
                    Fail("an intrinsic statement holds something else");
                }

                RvalueType(body, statement.Rvalue);
                break;
            case StatementKind.TracePush:
                break;
            default:
                if (statement.Aux < 0 || statement.Aux >= body.Locals.Count)
                {
                    Fail("a statement names a missing local");
                }

                break;
        }
    }

    private void VerifyTerminator(Body body, Terminator terminator)
    {
        switch (terminator.Kind)
        {
            case TermKind.Goto:
                RequireBlock(body, terminator.Then);
                break;
            case TermKind.Branch:
                RequireBool(body, terminator.Operand);
                RequireBlock(body, terminator.Then);
                RequireBlock(body, terminator.Else);
                break;
            case TermKind.Switch:
                TypeId selector = OperandType(body, terminator.Operand);
                if (selector.IsNone || !IrTypes.IsInteger(IrTypes.ScalarKind(module, selector)))
                {
                    Fail("switch on a non integer");
                }

                RequireBlock(body, terminator.Else);
                for (int i = 0; i < terminator.Cases.Count; i++)
                {
                    SwitchCase item = body.SwitchCases[terminator.Cases.Start + i];
                    RequireBlock(body, item.Target);
                    if (item.Value.Index < 0 || item.Value.Index >= module.Constants.Count
                        || module.Constants[item.Value.Index].Type != selector)
                    {
                        Fail("switch case of another type");
                    }
                }

                break;
            case TermKind.Return:
                bool isVoid = ValidType(_function.Return) && KindOf(_function.Return) == TypeKind.Void;
                if (terminator.Operand.IsNone)
                {
                    if (!isVoid)
                    {
                        Fail("return without a value in a function that returns one");
                    }
                }
                else if (OperandType(body, terminator.Operand) != _function.Return)
                {
                    Fail("return of the wrong type");
                }

                break;
            case TermKind.Unreachable:
                if (stage == ModuleStage.Final && terminator.Mode == UnreachableMode.Default)
                {
                    Fail("a default unreachable remains in the final stage");
                }

                break;
            default:
                OperandType(body, terminator.Operand);
                break;
        }
    }

    private void RequireBlock(Body body, BlockId block)
    {
        if (block.Index < 0 || block.Index >= body.Blocks.Count)
        {
            Fail("a branch targets a missing block");
        }
    }

    private void RequireBool(Body body, OperandId operand)
    {
        TypeId type = OperandType(body, operand);
        if (type.IsNone || KindOf(type) != TypeKind.Bool)
        {
            Fail("a condition is not a bool");
        }
    }

    private TypeId PlaceType(Body body, PlaceId place)
    {
        if (place.Index < 0 || place.Index >= body.Places.Count)
        {
            Fail("a place does not resolve");
            return TypeId.None;
        }

        Place p = body.Places[place.Index];
        if (p.Base == PlaceBase.Global ? p.Id < 0 || p.Id >= module.Globals.Count : p.Id < 0 || p.Id >= body.Locals.Count)
        {
            Fail("a place names a missing local or global");
            return TypeId.None;
        }

        TypeId current;
        switch (p.Base)
        {
            case PlaceBase.Local:
                current = body.Locals[p.Id].Type;
                break;
            case PlaceBase.Global:
                current = module.Globals[p.Id].Type;
                break;
            default:
                current = IrTyper.Pointee(module, body.Locals[p.Id].Type);
                if (current.IsNone)
                {
                    Fail("a deref place names a local that is not a pointer");
                    return TypeId.None;
                }

                break;
        }

        for (int i = 0; i < p.Projections.Count; i++)
        {
            Projection projection = body.Projections[p.Projections.Start + i];
            if (projection.Kind == ProjectionKind.Index)
            {
                TypeId index = OperandType(body, new OperandId(projection.Value));
                if (index.IsNone || !IrTypes.IsInteger(IrTypes.ScalarKind(module, index)))
                {
                    Fail("an index is not an integer");
                    return TypeId.None;
                }
            }

            current = IrTyper.Project(module, current, projection);
            if (current.IsNone)
            {
                Fail("a projection does not apply to its type");
                return TypeId.None;
            }
        }

        return current;
    }

    private TypeId OperandType(Body body, OperandId operand)
    {
        if (operand.Index < 0 || operand.Index >= body.Operands.Count)
        {
            Fail("an operand does not resolve");
            return TypeId.None;
        }

        Operand o = body.Operands[operand.Index];
        switch (o.Kind)
        {
            case OperandKind.Copy:
                return PlaceType(body, new PlaceId(o.A));
            case OperandKind.Const:
                if (o.A < 0 || o.A >= module.Constants.Count)
                {
                    Fail("a constant does not resolve");
                    return TypeId.None;
                }

                return module.Constants[o.A].Type;
            case OperandKind.FnRef:
                Fail("function references are not supported at this milestone");
                return TypeId.None;
            default:
                return ValidType(new TypeId(o.A)) ? new TypeId(o.A) : TypeId.None;
        }
    }

    private TypeId RvalueType(Body body, RvalueId rvalue)
    {
        if (rvalue.Index < 0 || rvalue.Index >= body.Rvalues.Count)
        {
            Fail("an rvalue does not resolve");
            return TypeId.None;
        }

        Rvalue r = body.Rvalues[rvalue.Index];
        switch (r.Kind)
        {
            case RvalueKind.Use:
                return OperandType(body, r.A);
            case RvalueKind.Binop:
                return BinopType(body, r);
            case RvalueKind.Unop:
                return UnopType(body, r);
            case RvalueKind.Cast:
                OperandType(body, r.A);
                if (stage == ModuleStage.Final && r.Mode == ArithMode.Default && IrNames.HasMode((CastKind)r.Op))
                {
                    Fail("a default mode remains in the final stage");
                }

                return ValidType(r.Type) ? r.Type : TypeId.None;
            case RvalueKind.Addr:
                PlaceType(body, r.Place);
                return IrTyper.OfRvalue(module, body, rvalue);
            case RvalueKind.Aggregate:
                return AggregateType(body, r);
            case RvalueKind.Len:
                TypeId measured = PlaceType(body, r.Place);
                if (measured.IsNone || KindOf(measured) is not (TypeKind.Array or TypeKind.Slice))
                {
                    Fail("len of something that is not an array or a slice");
                }

                return IrTyper.OfRvalue(module, body, rvalue);
            case RvalueKind.HasPayload:
                TypeId optional = PlaceType(body, r.Place);
                if (optional.IsNone || KindOf(optional) is not (TypeKind.Optional or TypeKind.Result))
                {
                    Fail("haspayload of something that is not an optional or a result");
                }

                return IrTyper.OfRvalue(module, body, rvalue);
            case RvalueKind.Slice:
                TypeId sliced = PlaceType(body, r.Place);
                if (sliced.IsNone || KindOf(sliced) is not (TypeKind.Array or TypeKind.Slice))
                {
                    Fail("slice of something that is not an array or a slice");
                    return TypeId.None;
                }

                RequireInteger(body, r.A, "a slice start is not an integer");
                RequireInteger(body, r.B, "a slice end is not an integer");
                return IrTyper.OfRvalue(module, body, rvalue);
            case RvalueKind.Alloca:
                if (!ValidType(r.Type))
                {
                    return TypeId.None;
                }

                RequireInteger(body, r.A, "an alloca count is not an integer");
                return IrTyper.OfRvalue(module, body, rvalue);
            case RvalueKind.OptionalSome:
                OperandType(body, r.A);
                return IrTyper.OfRvalue(module, body, rvalue);
            case RvalueKind.OptionalNone:
                return ValidType(r.Type) ? IrTyper.OfRvalue(module, body, rvalue) : TypeId.None;
            case RvalueKind.ResultOk:
                return ResultOkType(body, r);
            case RvalueKind.ResultErr:
                return ResultErrType(body, r);
            case RvalueKind.Err:
                TypeId failing = PlaceType(body, r.Place);
                if (failing.IsNone || KindOf(failing) != TypeKind.Result)
                {
                    Fail("err of something that is not a result");
                    return TypeId.None;
                }

                return IrTyper.OfRvalue(module, body, rvalue);
            case RvalueKind.Errcode:
                TypeId coded = OperandType(body, r.A);
                if (!coded.IsNone && KindOf(coded) is not (TypeKind.ErrorSet or TypeKind.AnyError))
                {
                    Fail("errcode of something that is not an error");
                }

                return IrTyper.OfRvalue(module, body, rvalue);
            case RvalueKind.Intrinsic:
                foreach (int arg in Enumerable.Range(r.Args.Start, r.Args.Count))
                {
                    OperandType(body, body.Args[arg]);
                }

                TypeId named = IrTyper.OfRvalue(module, body, rvalue);
                if (named.IsNone)
                {
                    Fail("unknown intrinsic " + module.Strings[r.Aux]);
                }

                return named;
            case RvalueKind.SizeOf:
            case RvalueKind.AlignOf:
            case RvalueKind.OffsetOf:
                return ValidType(r.Type) ? IrTyper.OfRvalue(module, body, rvalue) : TypeId.None;
            default:
                return CallType(body, r);
        }
    }

    private TypeId ResultOkType(Body body, Rvalue r)
    {
        if (!ValidType(r.Type) || KindOf(r.Type) != TypeKind.Result)
        {
            Fail("result.ok of a type that is not a result");
            return TypeId.None;
        }

        var payload = new TypeId(module.Types[r.Type.Index].A);
        bool isVoid = KindOf(payload) == TypeKind.Void;
        if (isVoid != r.A.IsNone)
        {
            Fail("result.ok needs a value exactly when the payload is not void");
        }
        else if (!isVoid && OperandType(body, r.A) != payload)
        {
            Fail("result.ok of the wrong type");
        }

        return r.Type;
    }

    private TypeId ResultErrType(Body body, Rvalue r)
    {
        if (!ValidType(r.Type) || KindOf(r.Type) != TypeKind.Result)
        {
            Fail("result.err of a type that is not a result");
            return TypeId.None;
        }

        TypeId error = OperandType(body, r.A);
        var target = new TypeId(module.Types[r.Type.Index].B);
        if (!error.IsNone && !ErrorFits(error, target))
        {
            Fail("result.err of an error the result cannot hold");
        }

        return r.Type;
    }

    private bool ErrorFits(TypeId source, TypeId target)
    {
        if (source == target || KindOf(target) == TypeKind.AnyError)
        {
            return KindOf(source) is TypeKind.ErrorSet or TypeKind.AnyError;
        }

        if (KindOf(source) != TypeKind.ErrorSet || KindOf(target) != TypeKind.ErrorSet)
        {
            return false;
        }

        HashSet<int> allowed = [.. module.TypeLists[module.Types[target.Index].A]];
        return module.TypeLists[module.Types[source.Index].A].All(allowed.Contains);
    }

    private bool ConstSliceOf(Body body, TypeId destination, RvalueId rvalue)
    {
        if (body.Rvalues[rvalue.Index].Kind != RvalueKind.Slice)
        {
            return false;
        }

        TypeNode wanted = module.Types[destination.Index];
        TypeId produced = IrTyper.OfRvalue(module, body, rvalue);
        return wanted.Kind == TypeKind.Slice && wanted.Flags.HasFlag(TypeFlags.Const) && !produced.IsNone
            && module.Types[produced.Index].A == wanted.A;
    }

    private void RequireInteger(Body body, OperandId operand, string message)
    {
        TypeId type = OperandType(body, operand);
        if (type.IsNone || !IrTypes.IsInteger(IrTypes.ScalarKind(module, type)))
        {
            Fail(message);
        }
    }

    private TypeId AggregateType(Body body, Rvalue r)
    {
        if (!ValidType(r.Type))
        {
            Fail("an aggregate type does not resolve");
            return TypeId.None;
        }

        TypeNode node = module.Types[r.Type.Index];
        var expected = new List<TypeId>();
        switch (node.Kind)
        {
            case TypeKind.Struct:
                StructDecl declaration = module.Structs[node.A];
                for (int i = 0; i < declaration.Fields.Count; i++)
                {
                    expected.Add(module.Fields[declaration.Fields.Start + i].Type);
                }

                break;
            case TypeKind.Array:
                for (int i = 0; i < node.B; i++)
                {
                    expected.Add(new TypeId(node.A));
                }

                break;
            case TypeKind.Slice:
                expected.Add(module.Type(new TypeNode(TypeKind.Ptr, TypeFlags.Nullable | TypeFlags.Many | (node.Flags & TypeFlags.Const), node.A, 0)));
                expected.Add(module.Type(new TypeNode(TypeKind.I32, TypeFlags.None, 0, 0)));
                break;
            default:
                Fail("an aggregate of a type that is not a struct, an array or a slice");
                return TypeId.None;
        }

        if (r.Args.Count != expected.Count)
        {
            Fail("an aggregate has the wrong number of elements");
            return r.Type;
        }

        for (int i = 0; i < expected.Count; i++)
        {
            if (OperandType(body, body.Args[r.Args.Start + i]) != expected[i])
            {
                Fail("an aggregate element has the wrong type");
            }
        }

        return r.Type;
    }

    private TypeId BinopType(Body body, Rvalue r)
    {
        TypeId left = OperandType(body, r.A);
        TypeId right = OperandType(body, r.B);
        if (left.IsNone || right.IsNone)
        {
            return TypeId.None;
        }

        var op = (BinOp)r.Op;
        if (KindOf(left) == TypeKind.Ptr)
        {
            return PointerBinop(op, left, right);
        }

        bool shift = op is BinOp.Shl or BinOp.Shr;
        if (!shift && left != right)
        {
            Fail("binary operation on different types");
        }

        TypeKind kind = IrTypes.ScalarKind(module, left);
        bool floatOp = op is >= BinOp.FAdd and <= BinOp.FRem or >= BinOp.FEq;
        bool boolOp = kind == TypeKind.Bool && op is BinOp.And or BinOp.Or or BinOp.Xor or BinOp.Eq or BinOp.Ne;
        bool valid = IrTypes.IsFloat(kind) ? floatOp : floatOp ? false : IrTypes.IsInteger(kind) || boolOp;
        if (!valid)
        {
            Fail("binary operation on an invalid type");
        }

        if (stage == ModuleStage.Final && r.Mode == ArithMode.Default && IrNames.HasMode(op))
        {
            Fail("a default mode remains in the final stage");
        }

        return op >= BinOp.Eq ? module.Type(new TypeNode(TypeKind.Bool, TypeFlags.None, 0, 0)) : left;
    }

    private TypeId PointerBinop(BinOp op, TypeId left, TypeId right)
    {
        TypeKind rightKind = KindOf(right);
        TypeId boolean = module.Type(new TypeNode(TypeKind.Bool, TypeFlags.None, 0, 0));
        if (op >= BinOp.Eq)
        {
            if (op is not (BinOp.Eq or BinOp.Ne) && rightKind != TypeKind.Ptr)
            {
                Fail("an ordering comparison of a pointer with a non pointer");
            }

            return boolean;
        }

        if (op is BinOp.Add or BinOp.Sub && rightKind == TypeKind.ISize)
        {
            return left;
        }

        if (op == BinOp.Sub && rightKind == TypeKind.Ptr)
        {
            return module.Type(new TypeNode(TypeKind.ISize, TypeFlags.None, 0, 0));
        }

        Fail("an invalid pointer operation");
        return TypeId.None;
    }

    private TypeId UnopType(Body body, Rvalue r)
    {
        TypeId operand = OperandType(body, r.A);
        if (operand.IsNone)
        {
            return TypeId.None;
        }

        var op = (UnOp)r.Op;
        TypeKind kind = IrTypes.ScalarKind(module, operand);
        bool valid = op switch
        {
            UnOp.Not => kind == TypeKind.Bool,
            UnOp.FNeg => IrTypes.IsFloat(kind),
            _ => IrTypes.IsInteger(kind),
        };
        if (!valid)
        {
            Fail("unary operation on an invalid type");
        }

        if (stage == ModuleStage.Final && r.Mode == ArithMode.Default && op == UnOp.Neg)
        {
            Fail("a default mode remains in the final stage");
        }

        return operand;
    }

    private TypeId CallType(Body body, Rvalue r)
    {
        SigNode sig;
        if (r.Callee == CalleeKind.Direct)
        {
            if (r.CalleeId < 0 || r.CalleeId >= module.Functions.Count)
            {
                Fail("a call names a missing function");
                return TypeId.None;
            }

            FunctionDecl callee = module.Functions[r.CalleeId];
            var parameters = new List<TypeId>();
            for (int i = 0; i < callee.Params.Count; i++)
            {
                parameters.Add(module.Params[callee.Params.Start + i].Type);
            }

            CheckArguments(body, r, parameters, callee.Variadic);
            return callee.Return;
        }

        TypeId calleeType = OperandType(body, new OperandId(r.CalleeId));
        if (calleeType.IsNone || KindOf(calleeType) != TypeKind.Fn)
        {
            Fail("an indirect call through a non function");
            return TypeId.None;
        }

        sig = module.Sigs[module.Types[calleeType.Index].A];
        var types = new List<TypeId>();
        foreach (int item in module.TypeLists[sig.Params.Index])
        {
            types.Add(SigParam.Decode(item).Type);
        }

        CheckArguments(body, r, types, sig.Variadic);
        return sig.Return;
    }

    private void CheckArguments(Body body, Rvalue r, List<TypeId> parameters, bool variadic)
    {
        if (r.Args.Count < parameters.Count || (!variadic && r.Args.Count != parameters.Count))
        {
            Fail("a call passes the wrong number of arguments");
            return;
        }

        for (int i = 0; i < r.Args.Count; i++)
        {
            TypeId argument = OperandType(body, body.Args[r.Args.Start + i]);
            if (i < parameters.Count && argument != parameters[i])
            {
                Fail("a call argument has the wrong type");
            }
        }
    }

    private void VerifyReachabilityAndAssignment(Body body)
    {
        int blocks = body.Blocks.Count;
        int locals = body.Locals.Count;
        var successors = new List<int>[blocks];
        for (int b = 0; b < blocks; b++)
        {
            successors[b] = SuccessorsOf(body, b);
        }

        bool[] reachable = new bool[blocks];
        var stack = new Stack<int>();
        stack.Push(0);
        reachable[0] = true;
        while (stack.Count > 0)
        {
            foreach (int next in successors[stack.Pop()])
            {
                if (next >= 0 && next < blocks && !reachable[next])
                {
                    reachable[next] = true;
                    stack.Push(next);
                }
            }
        }

        _span = _function.Span;
        for (int b = 0; b < blocks; b++)
        {
            if (!reachable[b])
            {
                Fail("bb" + b + " is not reachable");
            }
        }

        bool[] entry = new bool[locals];
        for (int i = 0; i < locals; i++)
        {
            entry[i] = body.Locals[i].Kind == LocalKind.Param;
        }

        bool[][] inState = new bool[blocks][];
        for (int b = 0; b < blocks; b++)
        {
            inState[b] = new bool[locals];
            Array.Fill(inState[b], b != 0);
        }

        entry.CopyTo(inState[0], 0);

        bool changed = true;
        while (changed)
        {
            changed = false;
            for (int b = 0; b < blocks; b++)
            {
                if (!reachable[b])
                {
                    continue;
                }

                bool[] outState = Transfer(body, b, inState[b], report: false);
                foreach (int next in successors[b])
                {
                    if (next < 0 || next >= blocks)
                    {
                        continue;
                    }

                    for (int l = 0; l < locals; l++)
                    {
                        if (inState[next][l] && !outState[l])
                        {
                            inState[next][l] = false;
                            changed = true;
                        }
                    }
                }
            }
        }

        for (int b = 0; b < blocks; b++)
        {
            if (reachable[b])
            {
                Transfer(body, b, inState[b], report: true);
            }
        }
    }

    private static List<int> SuccessorsOf(Body body, int block)
    {
        var result = new List<int>();
        if (body.Blocks[block].Terminator is not Terminator t)
        {
            return result;
        }

        switch (t.Kind)
        {
            case TermKind.Goto:
                result.Add(t.Then.Index);
                break;
            case TermKind.Branch:
                result.Add(t.Then.Index);
                result.Add(t.Else.Index);
                break;
            case TermKind.Switch:
                for (int i = 0; i < t.Cases.Count; i++)
                {
                    result.Add(body.SwitchCases[t.Cases.Start + i].Target.Index);
                }

                result.Add(t.Else.Index);
                break;
        }

        return result;
    }

    private bool[] Transfer(Body body, int block, bool[] input, bool report)
    {
        bool[] state = (bool[])input.Clone();
        foreach (StatementId statementId in body.Blocks[block].Statements)
        {
            Statement statement = body.Statements[statementId.Index];
            _span = statement.Span;
            switch (statement.Kind)
            {
                case StatementKind.Assign:
                    ReadRvalue(body, statement.Rvalue, state, report);
                    Place destination = body.Places[statement.Place.Index];
                    ReadPlaceOperands(body, destination, state, report);
                    if (destination.Base == PlaceBase.Local && destination.Projections.Count == 0)
                    {
                        state[destination.Id] = true;
                    }
                    else if (destination.Base == PlaceBase.Local)
                    {
                        state[destination.Id] = true;
                    }
                    else if (destination.Base == PlaceBase.DerefLocal && report && !state[destination.Id])
                    {
                        Fail("local %" + destination.Id + " is dereferenced before it is assigned");
                    }

                    break;
                case StatementKind.Call:
                case StatementKind.Intrinsic:
                    ReadRvalue(body, statement.Rvalue, state, report);
                    break;
                case StatementKind.Check:
                    ReadOperand(body, statement.Operand, state, report);
                    break;
                case StatementKind.TraceSave:
                    state[statement.Aux] = true;
                    break;
                case StatementKind.TraceRestore:
                    if (report && !state[statement.Aux])
                    {
                        Fail("local %" + statement.Aux + " is restored before it is saved");
                    }

                    break;
            }
        }

        if (body.Blocks[block].Terminator is Terminator terminator)
        {
            _span = terminator.Span;
            if (!terminator.Operand.IsNone)
            {
                ReadOperand(body, terminator.Operand, state, report);
            }
        }

        return state;
    }

    private void ReadRvalue(Body body, RvalueId id, bool[] state, bool report)
    {
        Rvalue r = body.Rvalues[id.Index];
        if (!r.Place.IsNone)
        {
            Place addressed = body.Places[r.Place.Index];
            if (r.Kind != RvalueKind.Addr)
            {
                ReadPlaceBase(body, addressed, state, report);
            }

            ReadPlaceOperands(body, addressed, state, report);
            if (r.Kind == RvalueKind.Addr && addressed.Base == PlaceBase.Local)
            {
                state[addressed.Id] = true;
            }
        }

        if (!r.A.IsNone)
        {
            ReadOperand(body, r.A, state, report);
        }

        if (!r.B.IsNone)
        {
            ReadOperand(body, r.B, state, report);
        }

        if (r.Kind is RvalueKind.Call or RvalueKind.Aggregate or RvalueKind.Intrinsic)
        {
            if (r.Kind == RvalueKind.Call && r.Callee == CalleeKind.Indirect)
            {
                ReadOperand(body, new OperandId(r.CalleeId), state, report);
            }

            for (int i = 0; i < r.Args.Count; i++)
            {
                ReadOperand(body, body.Args[r.Args.Start + i], state, report);
            }
        }
    }

    private void ReadPlaceBase(Body body, Place place, bool[] state, bool report)
    {
        if (place.Base == PlaceBase.Global)
        {
            return;
        }

        if (report && !state[place.Id] && place.Base == PlaceBase.DerefLocal)
        {
            Fail("local %" + place.Id + " is used before it is assigned");
        }
    }

    private void ReadPlaceOperands(Body body, Place place, bool[] state, bool report)
    {
        for (int i = 0; i < place.Projections.Count; i++)
        {
            Projection projection = body.Projections[place.Projections.Start + i];
            if (projection.Kind == ProjectionKind.Index)
            {
                ReadOperand(body, new OperandId(projection.Value), state, report);
            }
        }
    }

    private void ReadOperand(Body body, OperandId id, bool[] state, bool report)
    {
        Operand operand = body.Operands[id.Index];
        if (operand.Kind != OperandKind.Copy)
        {
            return;
        }

        Place place = body.Places[operand.A];
        ReadPlaceOperands(body, place, state, report);
        if (place.Base == PlaceBase.Global)
        {
            return;
        }

        if (report && !state[place.Id])
        {
            Fail("local %" + place.Id + " is used before it is assigned");
        }
    }
}
