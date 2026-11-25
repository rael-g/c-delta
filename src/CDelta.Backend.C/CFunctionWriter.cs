using System.Text;
using System.Text.RegularExpressions;
using CDelta.Ir;

namespace CDelta.Backend.C;

internal sealed partial class CFunctionWriter(
    Module module,
    FunctionId id,
    CNames names,
    BackendOptions options,
    CEmitState state)
{
    private const string Indent = "    ";
    private const string UnknownFile = "<unknown>";
    private const string IntegerOverflow = "integer overflow";
    private const string CastTruncated = "integer cast truncated bits";
    private const string Unreachable = "reached unreachable code";

    private readonly FunctionDecl _function = module.Functions[id.Index];
    private readonly Body _body = module.Bodies[id.Index]!;
    private readonly StringBuilder _text = new();
    private string[] _locals = [];

    [GeneratedRegex(@"^[A-Za-z_][A-Za-z0-9_]*$")]
    private static partial Regex Identifier();

    public static string Signature(Module module, FunctionId id, CNames names, bool withNames, string[]? localNames)
    {
        FunctionDecl function = module.Functions[id.Index];
        var text = new StringBuilder();
        if (function.Linkage == LinkageKind.Internal)
        {
            text.Append("static ");
        }

        text.Append(CTypes.Spell(module, names, function.Return)).Append(' ').Append(names.Function(id)).Append('(');
        if (function.Params.Count == 0 && !function.Variadic)
        {
            text.Append("void");
        }

        for (int i = 0; i < function.Params.Count; i++)
        {
            if (i > 0)
            {
                text.Append(", ");
            }

            text.Append(CTypes.Spell(module, names, module.Params[function.Params.Start + i].Type));
            if (withNames)
            {
                text.Append(' ').Append(localNames![i]);
            }
        }

        if (function.Variadic)
        {
            text.Append(function.Params.Count > 0 ? ", ..." : "...");
        }

        return text.Append(')').ToString();
    }

    public string Write()
    {
        _locals = NameLocals();
        _text.Append(Signature(module, id, names, true, _locals)).Append('\n').Append("{\n");
        for (int i = _function.Params.Count; i < _body.Locals.Count; i++)
        {
            TypeId type = _body.Locals[i].Type;
            if (module.Types[type.Index].Kind != TypeKind.Void)
            {
                _text.Append(Indent).Append(CTypes.Spell(module, names, type)).Append(' ').Append(_locals[i]).Append(";\n");
            }
        }

        HashSet<int> targets = BranchTargets();
        for (int b = 0; b < _body.Blocks.Count; b++)
        {
            if (targets.Contains(b))
            {
                _text.Append("bb").Append(b).Append(":\n");
            }

            Block block = _body.Blocks[b];
            foreach (StatementId statementId in block.Statements)
            {
                WriteStatement(_body.Statements[statementId.Index]);
            }

            WriteTerminator(block.Terminator!.Value);
        }

        return _text.Append("}\n").ToString();
    }

    private string[] NameLocals()
    {
        string[] result = new string[_body.Locals.Count];
        var used = new HashSet<string>();
        for (int i = 0; i < result.Length; i++)
        {
            LocalDecl local = _body.Locals[i];
            string candidate = local.Name.IsNone ? string.Empty : module.Text(local.Name);
            bool valid = candidate.Length > 0
                && Identifier().IsMatch(candidate)
                && !candidate.StartsWith("cd_", StringComparison.Ordinal)
                && !names.IsTaken(candidate)
                && !used.Contains(candidate);
            string name = valid ? candidate : (local.Kind == LocalKind.Temp ? "t" : "l") + i;
            used.Add(name);
            result[i] = name;
        }

        return result;
    }

    private HashSet<int> BranchTargets()
    {
        var targets = new HashSet<int>();
        foreach (Block block in _body.Blocks)
        {
            if (block.Terminator is not Terminator t)
            {
                continue;
            }

            switch (t.Kind)
            {
                case TermKind.Goto:
                    targets.Add(t.Then.Index);
                    break;
                case TermKind.Branch:
                    targets.Add(t.Then.Index);
                    targets.Add(t.Else.Index);
                    break;
            }
        }

        return targets;
    }

    private void Line(SourceSpan span)
    {
        if (options.LineDirectives && !span.IsNone)
        {
            string path = module.Sources[span.File.Index].FullPath.Replace("\\", "\\\\", StringComparison.Ordinal);
            _text.Append("#line ").Append(span.Line).Append(" \"").Append(path).Append("\"\n");
        }
    }

    private string DisplayPath(SourceSpan span) =>
        span.IsNone ? UnknownFile : module.Sources[span.File.Index].DisplayPath;

    private string PanicCall(string message, SourceSpan span)
    {
        state.UsesPanic = true;
        byte[] bytes = Encoding.UTF8.GetBytes(message);
        return "cd_panic((const uint8_t*)" + CConstants.StringLiteral(bytes) + ", " + bytes.Length + ", "
            + CConstants.StringLiteral(Encoding.UTF8.GetBytes(DisplayPath(span))) + ", " + span.Line + ");";
    }

    private string Place(PlaceId id) => _locals[_body.Places[id.Index].Id];

    private string OperandText(OperandId id)
    {
        Operand operand = _body.Operands[id.Index];
        return operand.Kind switch
        {
            OperandKind.Copy => Place(new PlaceId(operand.A)),
            OperandKind.Const => CConstants.Spell(module, names, new ConstId(operand.A)),
            _ => throw new NotSupportedException("the operand kind " + operand.Kind + " is not supported at this stage"),
        };
    }

    private TypeKind KindOf(TypeId type) => module.Types[type.Index].Kind;

    private void WriteStatement(Statement statement)
    {
        Line(statement.Span);
        switch (statement.Kind)
        {
            case StatementKind.Assign:
                WriteAssign(statement);
                break;
            case StatementKind.Call:
                _text.Append(Indent).Append(CallText(_body.Rvalues[statement.Rvalue.Index])).Append(";\n");
                break;
            case StatementKind.Check:
                _text.Append(Indent).Append("if (__builtin_expect(!(").Append(OperandText(statement.Operand)).Append("), 0)) ")
                    .Append(PanicCall(module.Text(statement.Message), statement.Span)).Append('\n');
                break;
        }
    }

    private string CallText(Rvalue rvalue)
    {
        var text = new StringBuilder();
        if (rvalue.Callee == CalleeKind.Direct)
        {
            text.Append(names.Function(new FunctionId(rvalue.CalleeId)));
        }
        else
        {
            text.Append("(*").Append(OperandText(new OperandId(rvalue.CalleeId))).Append(')');
        }

        text.Append('(');
        for (int i = 0; i < rvalue.Args.Count; i++)
        {
            if (i > 0)
            {
                text.Append(", ");
            }

            text.Append(OperandText(_body.Args[rvalue.Args.Start + i]));
        }

        return text.Append(')').ToString();
    }

    private void WriteAssign(Statement statement)
    {
        Rvalue rvalue = _body.Rvalues[statement.Rvalue.Index];
        string destination = Place(statement.Place);
        TypeId destinationType = IrTyper.OfPlace(module, _body, statement.Place);
        string cType = CTypes.Spell(module, names, destinationType);
        TypeKind kind = KindOf(destinationType);
        switch (rvalue.Kind)
        {
            case RvalueKind.Use:
                Statement(destination + " = " + OperandText(rvalue.A) + ";");
                break;
            case RvalueKind.Call:
                Statement(destination + " = " + CallText(rvalue) + ";");
                break;
            case RvalueKind.Binop:
                WriteBinop(rvalue, destination, cType, statement.Span);
                break;
            case RvalueKind.Unop:
                WriteUnop(rvalue, destination, cType, kind, statement.Span);
                break;
            default:
                WriteCast(rvalue, destination, cType, statement.Span);
                break;
        }
    }

    private void Statement(string text) => _text.Append(Indent).Append(text).Append('\n');

    private void WriteBinop(Rvalue rvalue, string destination, string cType, SourceSpan span)
    {
        var op = (BinOp)rvalue.Op;
        string a = OperandText(rvalue.A);
        string b = OperandText(rvalue.B);
        TypeKind operandKind = KindOf(IrTyper.OfOperand(module, _body, rvalue.A));
        if (op >= BinOp.Eq)
        {
            Statement(destination + " = (" + a + " " + CompareOperator(op) + " " + b + ");");
            return;
        }

        if (operandKind == TypeKind.Bool)
        {
            Statement(destination + " = (" + cType + ")(" + a + " " + BitwiseOperator(op) + " " + b + ");");
            return;
        }

        string unsignedType = CTypes.Unsigned(operandKind);
        int width = IrTypes.BitWidth(operandKind, module.Target!.Value);
        switch (op)
        {
            case BinOp.Add or BinOp.Sub or BinOp.Mul:
                WriteArithmetic(op, rvalue.Mode, destination, cType, unsignedType, a, b, span);
                break;
            case BinOp.Div:
                Statement(destination + " = (" + cType + ")(" + a + " / " + b + ");");
                break;
            case BinOp.Rem:
                Statement(destination + " = (" + cType + ")(" + a + " % " + b + ");");
                break;
            case BinOp.Shl:
                Statement(destination + " = (" + cType + ")((" + unsignedType + ")" + a + " << " + ShiftCount(b, width) + ");");
                break;
            case BinOp.Shr:
                Statement(destination + " = (" + cType + ")(" + a + " >> " + ShiftCount(b, width) + ");");
                break;
            default:
                Statement(destination + " = (" + cType + ")(" + a + " " + BitwiseOperator(op) + " " + b + ");");
                break;
        }
    }

    private static string ShiftCount(string count, int width) => "((uint32_t)(" + count + ") & " + (width - 1) + "u)";

    private static string CompareOperator(BinOp op) => op switch
    {
        BinOp.Eq => "==",
        BinOp.Ne => "!=",
        BinOp.Lt => "<",
        BinOp.Le => "<=",
        BinOp.Gt => ">",
        _ => ">=",
    };

    private static string BitwiseOperator(BinOp op) => op switch
    {
        BinOp.And => "&",
        BinOp.Or => "|",
        _ => "^",
    };

    private static string ArithmeticOperator(BinOp op) => op switch
    {
        BinOp.Add => "+",
        BinOp.Sub => "-",
        _ => "*",
    };

    private static string OverflowBuiltin(BinOp op) => op switch
    {
        BinOp.Add => "__builtin_add_overflow",
        BinOp.Sub => "__builtin_sub_overflow",
        _ => "__builtin_mul_overflow",
    };

    private void WriteArithmetic(BinOp op, ArithMode mode, string destination, string cType, string unsignedType, string a, string b, SourceSpan span)
    {
        string symbol = ArithmeticOperator(op);
        switch (mode)
        {
            case ArithMode.Trap:
                Statement("if (__builtin_expect(" + OverflowBuiltin(op) + "(" + a + ", " + b + ", &" + destination + "), 0)) " + PanicCall(IntegerOverflow, span));
                break;
            case ArithMode.Wrap:
                Statement(destination + " = (" + cType + ")(" + unsignedType + ")((" + unsignedType + ")" + a + " " + symbol + " (" + unsignedType + ")" + b + ");");
                break;
            default:
                Statement(destination + " = (" + cType + ")(" + a + " " + symbol + " " + b + ");");
                break;
        }
    }

    private void WriteUnop(Rvalue rvalue, string destination, string cType, TypeKind kind, SourceSpan span)
    {
        string a = OperandText(rvalue.A);
        switch ((UnOp)rvalue.Op)
        {
            case UnOp.Not:
                Statement(destination + " = !" + a + ";");
                break;
            case UnOp.BitNot:
                Statement(destination + " = (" + cType + ")(~" + a + ");");
                break;
            default:
                string unsignedType = CTypes.Unsigned(kind);
                switch (rvalue.Mode)
                {
                    case ArithMode.Trap:
                        Statement("if (__builtin_expect(__builtin_sub_overflow((" + cType + ")0, " + a + ", &" + destination + "), 0)) " + PanicCall(IntegerOverflow, span));
                        break;
                    case ArithMode.Wrap:
                        Statement(destination + " = (" + cType + ")(" + unsignedType + ")(0u - (" + unsignedType + ")" + a + ");");
                        break;
                    default:
                        Statement(destination + " = (" + cType + ")(-" + a + ");");
                        break;
                }

                break;
        }
    }

    private void WriteCast(Rvalue rvalue, string destination, string cType, SourceSpan span)
    {
        var kind = (CastKind)rvalue.Op;
        string a = OperandText(rvalue.A);
        switch (kind)
        {
            case CastKind.PtrPtr:
                Statement(destination + " = (" + cType + ")" + a + ";");
                return;
            case CastKind.IntInt:
                break;
            default:
                throw new NotSupportedException("the cast " + kind + " is not supported at this stage");
        }

        TypeId sourceType = IrTyper.OfOperand(module, _body, rvalue.A);
        TypeKind from = KindOf(sourceType);
        TypeKind to = KindOf(rvalue.Type);
        Statement(destination + " = (" + cType + ")" + a + ";");
        if (rvalue.Mode == ArithMode.Trap && !Fits(from, to))
        {
            string source = CTypes.Spell(module, names, sourceType);
            Statement("if (__builtin_expect((((" + source + ")" + destination + " != " + a + ") || ((" + destination + " < 0) != (" + a + " < 0))), 0)) " + PanicCall(CastTruncated, span));
        }
    }

    private bool Fits(TypeKind from, TypeKind to)
    {
        Target target = module.Target!.Value;
        bool fromSigned = IrTypes.IsSigned(from, target);
        bool toSigned = IrTypes.IsSigned(to, target);
        int fromBits = IrTypes.BitWidth(from, target);
        int toBits = IrTypes.BitWidth(to, target);
        if (fromSigned == toSigned)
        {
            return toBits >= fromBits;
        }

        return !fromSigned && toBits > fromBits;
    }

    private void WriteTerminator(Terminator terminator)
    {
        Line(terminator.Span);
        switch (terminator.Kind)
        {
            case TermKind.Goto:
                Statement("goto bb" + terminator.Then.Index + ";");
                break;
            case TermKind.Branch:
                Statement("if (" + OperandText(terminator.Operand) + ") goto bb" + terminator.Then.Index + "; else goto bb" + terminator.Else.Index + ";");
                break;
            case TermKind.Return:
                Statement(terminator.Operand.IsNone ? "return;" : "return " + OperandText(terminator.Operand) + ";");
                break;
            case TermKind.Unreachable:
                Statement(terminator.Mode == UnreachableMode.Assume ? "__builtin_unreachable();" : PanicCall(Unreachable, terminator.Span));
                break;
            default:
                throw new NotSupportedException("the terminator " + terminator.Kind + " is not supported at this stage");
        }
    }
}
