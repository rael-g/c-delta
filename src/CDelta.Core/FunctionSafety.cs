using CDelta.Ir;

namespace CDelta.Core;

internal sealed class FunctionSafety(Module module, Body body, Target target, bool checksOn)
{
    private const string DivisionByZero = "division by zero";
    private const string IntegerOverflow = "integer overflow";

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

                rewritten.Add(id);
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
