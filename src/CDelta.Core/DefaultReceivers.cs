using CDelta.Ir;

namespace CDelta.Core;

public sealed partial class Instantiator
{
    private const int ReceiverArgument = 0;

    private HashSet<int>? _defaultFunctions;
    private bool _resolvedToDefault;

    private bool IsDefaultFunction(FunctionId function)
    {
        _defaultFunctions ??= [.. _source.InterfaceMethods.Where(method => !method.Default.IsNone).Select(method => method.Default.Index)];
        return _defaultFunctions.Contains(function.Index);
    }

    private void CopyDefaultReceivers(Body body, List<int> calls, bool insideDefault)
    {
        var builder = new FunctionBuilder(_destination, body);
        foreach (int call in calls)
        {
            Rvalue rvalue = body.Rvalues[call];
            int slot = rvalue.Args.Start + ReceiverArgument;
            OperandId receiver = body.Args[slot];
            if (insideDefault && IsThisOfFunction(body, receiver))
            {
                continue;
            }

            (int block, int position) = FindStatement(body, call);
            Statement statement = body.Statements[body.Blocks[block].Statements[position].Index];
            TypeId pointer = IrTyper.OfOperand(_destination, body, receiver);
            TypeId value = IrTyper.Pointee(_destination, pointer);
            TypeId mutablePointer = _builder.Pointer(value, TypeFlags.None);
            LocalId source = builder.NewLocal(null, pointer, LocalKind.Temp, statement.Span);
            LocalId copy = builder.NewLocal(null, value, LocalKind.Temp, statement.Span);
            LocalId passed = builder.NewLocal(null, mutablePointer, LocalKind.Temp, statement.Span);
            var inserted = new List<StatementId>();
            builder.RedirectStatements(inserted);
            builder.SetBlock(new BlockId(block));
            builder.Assign(builder.LocalPlace(source), builder.Use(receiver), statement.Span);
            builder.Assign(builder.LocalPlace(copy), builder.Use(builder.Copy(builder.DerefPlace(source))), statement.Span);
            builder.Assign(builder.LocalPlace(passed), builder.Addr(builder.LocalPlace(copy), true), statement.Span);
            builder.RedirectStatements(null);
            body.Blocks[block].Statements.InsertRange(position, inserted);
            body.Args[slot] = builder.Copy(builder.LocalPlace(passed));
        }
    }

    private static (int Block, int Position) FindStatement(Body body, int rvalue)
    {
        for (int block = 0; block < body.Blocks.Count; block++)
        {
            List<StatementId> statements = body.Blocks[block].Statements;
            for (int position = 0; position < statements.Count; position++)
            {
                if (body.Statements[statements[position].Index].Rvalue.Index == rvalue)
                {
                    return (block, position);
                }
            }
        }

        throw new InvalidOperationException("a call has no statement");
    }

    private static bool IsThisOfFunction(Body body, OperandId receiver)
    {
        Operand operand = body.Operands[receiver.Index];
        if (operand.Kind != OperandKind.Copy || body.Places[operand.A] is not { Base: PlaceBase.Local, Projections.Count: 0 } held)
        {
            return false;
        }

        foreach (Statement statement in body.Statements)
        {
            if (statement.Kind != StatementKind.Assign || body.Places[statement.Place.Index] is not { Base: PlaceBase.Local, Projections.Count: 0 } target || target.Id != held.Id)
            {
                continue;
            }

            Rvalue rvalue = body.Rvalues[statement.Rvalue.Index];
            return rvalue.Kind == RvalueKind.Addr
                && body.Places[rvalue.Place.Index] is { Base: PlaceBase.DerefLocal, Projections.Count: 0 } through
                && body.Locals[through.Id].Kind == LocalKind.Param
                && through.Id == 0;
        }

        return false;
    }
}
