using CDelta.Ir;

namespace CDelta.Core;

internal static class TypeTestFolding
{
    public static void Fold(Module module, Body body)
    {
        var builder = new ModuleBuilder(module);
        TypeId boolean = builder.Primitive(TypeKind.Bool);
        bool folded = false;
        for (int i = 0; i < body.Rvalues.Count; i++)
        {
            Rvalue rvalue = body.Rvalues[i];
            if (rvalue.Kind != RvalueKind.Binop || (BinOp)rvalue.Op is not (BinOp.Eq or BinOp.Ne)
                || !TryTypeOf(module, body, rvalue.A, out Int128 left) || !TryTypeOf(module, body, rvalue.B, out Int128 right))
            {
                continue;
            }

            bool value = (left == right) == ((BinOp)rvalue.Op == BinOp.Eq);
            body.Operands.Add(new Operand(OperandKind.Const, builder.BoolConstant(boolean, value).Index));
            body.Rvalues[i] = rvalue with { Kind = RvalueKind.Use, Op = 0, A = new OperandId(body.Operands.Count - 1), B = OperandId.None };
            folded = true;
        }

        if (!folded)
        {
            return;
        }

        Dictionary<int, bool> known = KnownLocals(module, body);
        for (int i = 0; i < body.Blocks.Count; i++)
        {
            Terminator? terminator = body.Blocks[i].Terminator;
            if (terminator is { Kind: TermKind.Branch } branch
                && TryKnown(body, known, branch.Operand, out bool taken))
            {
                body.Blocks[i] = body.Blocks[i] with
                {
                    Terminator = branch with { Kind = TermKind.Goto, Operand = OperandId.None, Then = taken ? branch.Then : branch.Else, Else = BlockId.None },
                };
            }
        }

        RemoveUnreachable(body);
    }

    private static bool TryTypeOf(Module module, Body body, OperandId operand, out Int128 type)
    {
        type = 0;
        Operand item = body.Operands[operand.Index];
        if (item.Kind != OperandKind.Const || module.Constants[item.A].Kind != ConstKind.Type)
        {
            return false;
        }

        type = module.Constants[item.A].Value;
        return true;
    }

    private static Dictionary<int, bool> KnownLocals(Module module, Body body)
    {
        var assignments = new Dictionary<int, int>();
        var values = new Dictionary<int, bool>();
        foreach (Block block in body.Blocks)
        {
            foreach (StatementId id in block.Statements)
            {
                Statement statement = body.Statements[id.Index];
                if (statement.Kind != StatementKind.Assign || body.Places[statement.Place.Index] is not { Base: PlaceBase.Local, Projections.Count: 0 } place)
                {
                    continue;
                }

                assignments[place.Id] = assignments.GetValueOrDefault(place.Id) + 1;
                Rvalue rvalue = body.Rvalues[statement.Rvalue.Index];
                if (rvalue.Kind == RvalueKind.Use
                    && body.Operands[rvalue.A.Index] is { Kind: OperandKind.Const } constant
                    && module.Constants[constant.A].Kind == ConstKind.Bool)
                {
                    values[place.Id] = module.Constants[constant.A].Value != 0;
                }
            }
        }

        return values.Where(pair => assignments[pair.Key] == 1).ToDictionary(pair => pair.Key, pair => pair.Value);
    }

    private static bool TryKnown(Body body, Dictionary<int, bool> known, OperandId operand, out bool value)
    {
        value = false;
        Operand item = body.Operands[operand.Index];
        return item.Kind == OperandKind.Copy
            && body.Places[item.A] is { Base: PlaceBase.Local, Projections.Count: 0 } place
            && known.TryGetValue(place.Id, out value);
    }

    private static void RemoveUnreachable(Body body)
    {
        bool[] reached = new bool[body.Blocks.Count];
        var pending = new Stack<int>();
        pending.Push(0);
        while (pending.Count > 0)
        {
            int index = pending.Pop();
            if (reached[index])
            {
                continue;
            }

            reached[index] = true;
            if (body.Blocks[index].Terminator is not { } terminator)
            {
                continue;
            }

            foreach (BlockId target in Targets(body, terminator))
            {
                pending.Push(target.Index);
            }
        }

        int[] renumbered = new int[reached.Length];
        var kept = new List<Block>();
        for (int i = 0; i < reached.Length; i++)
        {
            renumbered[i] = kept.Count;
            if (reached[i])
            {
                kept.Add(body.Blocks[i]);
            }
        }

        BlockId Map(BlockId block) => block.IsNone ? block : new BlockId(renumbered[block.Index]);
        for (int i = 0; i < kept.Count; i++)
        {
            if (kept[i].Terminator is { } terminator)
            {
                kept[i] = kept[i] with { Terminator = terminator with { Then = Map(terminator.Then), Else = Map(terminator.Else) } };
            }
        }

        for (int i = 0; i < body.SwitchCases.Count; i++)
        {
            body.SwitchCases[i] = body.SwitchCases[i] with { Target = Map(body.SwitchCases[i].Target) };
        }

        body.Blocks.Clear();
        body.Blocks.AddRange(kept);
    }

    private static IEnumerable<BlockId> Targets(Body body, Terminator terminator)
    {
        switch (terminator.Kind)
        {
            case TermKind.Goto:
                yield return terminator.Then;
                break;
            case TermKind.Branch:
                yield return terminator.Then;
                yield return terminator.Else;
                break;
            case TermKind.Switch:
                for (int i = 0; i < terminator.Cases.Count; i++)
                {
                    yield return body.SwitchCases[terminator.Cases.Start + i].Target;
                }

                yield return terminator.Else;
                break;
        }
    }
}
