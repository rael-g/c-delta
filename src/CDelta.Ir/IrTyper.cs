namespace CDelta.Ir;

public static class IrTyper
{
    public static TypeId OfPlace(Module module, Body body, PlaceId place)
    {
        Place p = body.Places[place.Index];
        if (p.Base != PlaceBase.Local || p.Projections.Count != 0)
        {
            return TypeId.None;
        }

        return body.Locals[p.Id].Type;
    }

    public static TypeId OfConstant(Module module, ConstId constant) => module.Constants[constant.Index].Type;

    public static TypeId OfOperand(Module module, Body body, OperandId operand)
    {
        Operand o = body.Operands[operand.Index];
        return o.Kind switch
        {
            OperandKind.Copy => OfPlace(module, body, new PlaceId(o.A)),
            OperandKind.Const => OfConstant(module, new ConstId(o.A)),
            OperandKind.Undef => new TypeId(o.A),
            _ => TypeId.None,
        };
    }

    public static TypeId OfRvalue(Module module, Body body, RvalueId rvalue)
    {
        Rvalue r = body.Rvalues[rvalue.Index];
        switch (r.Kind)
        {
            case RvalueKind.Use:
                return OfOperand(module, body, r.A);
            case RvalueKind.Binop:
                var op = (BinOp)r.Op;
                return op >= BinOp.Eq ? module.Type(new TypeNode(TypeKind.Bool, TypeFlags.None, 0, 0)) : OfOperand(module, body, r.A);
            case RvalueKind.Unop:
                return OfOperand(module, body, r.A);
            case RvalueKind.Cast:
                return r.Type;
            case RvalueKind.Call:
                if (r.Callee == CalleeKind.Direct)
                {
                    return module.Functions[r.CalleeId].Return;
                }

                TypeId calleeType = OfOperand(module, body, new OperandId(r.CalleeId));
                TypeNode node = module.Types[calleeType.Index];
                return node.Kind == TypeKind.Fn ? module.Sigs[node.A].Return : TypeId.None;
            default:
                return TypeId.None;
        }
    }
}
