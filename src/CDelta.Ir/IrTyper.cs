namespace CDelta.Ir;

public static class IrTyper
{
    public const string ErrorNameIntrinsic = "error.name";
    public const string ErrorReportIntrinsic = "error.report";
    public const string TextEqlIntrinsic = "text.eql";
    public const string Utf8NextIntrinsic = "utf8.next";

    public static TypeId Pointee(Module module, TypeId pointer)
    {
        if (pointer.IsNone || module.Types[pointer.Index].Kind != TypeKind.Ptr)
        {
            return TypeId.None;
        }

        return new TypeId(module.Types[pointer.Index].A);
    }

    public static TypeId OfPlace(Module module, Body body, PlaceId place)
    {
        Place p = body.Places[place.Index];
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
                current = Pointee(module, body.Locals[p.Id].Type);
                break;
        }

        for (int i = 0; i < p.Projections.Count && !current.IsNone; i++)
        {
            Projection projection = body.Projections[p.Projections.Start + i];
            current = Project(module, current, projection);
        }

        return current;
    }

    public static TypeId Project(Module module, TypeId type, Projection projection)
    {
        TypeNode node = module.Types[type.Index];
        switch (projection.Kind)
        {
            case ProjectionKind.Field:
                if (node.Kind != TypeKind.Struct)
                {
                    return TypeId.None;
                }

                StructDecl declaration = module.Structs[node.A];
                return projection.Value >= 0 && projection.Value < declaration.Fields.Count
                    ? module.Fields[declaration.Fields.Start + projection.Value].Type
                    : TypeId.None;
            case ProjectionKind.Index:
                return node.Kind is TypeKind.Array or TypeKind.Slice ? new TypeId(node.A) : TypeId.None;
            case ProjectionKind.Payload:
                return node.Kind is TypeKind.Optional or TypeKind.Result && projection.Value == 0 ? new TypeId(node.A) : TypeId.None;
            default:
                return TypeId.None;
        }
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
        TypeId boolean = module.Type(new TypeNode(TypeKind.Bool, TypeFlags.None, 0, 0));
        TypeId i32 = module.Type(new TypeNode(TypeKind.I32, TypeFlags.None, 0, 0));
        switch (r.Kind)
        {
            case RvalueKind.Use:
                return OfOperand(module, body, r.A);
            case RvalueKind.Binop:
                var op = (BinOp)r.Op;
                if (op >= BinOp.Eq)
                {
                    return boolean;
                }

                TypeId left = OfOperand(module, body, r.A);
                if (!left.IsNone && module.Types[left.Index].Kind == TypeKind.Ptr && op == BinOp.Sub)
                {
                    TypeId right = OfOperand(module, body, r.B);
                    if (!right.IsNone && module.Types[right.Index].Kind == TypeKind.Ptr)
                    {
                        return module.Type(new TypeNode(TypeKind.ISize, TypeFlags.None, 0, 0));
                    }
                }

                return left;
            case RvalueKind.Unop:
                return OfOperand(module, body, r.A);
            case RvalueKind.Cast:
            case RvalueKind.Aggregate:
                return r.Type;
            case RvalueKind.Addr:
                TypeId target = OfPlace(module, body, r.Place);
                return target.IsNone
                    ? TypeId.None
                    : module.Type(new TypeNode(TypeKind.Ptr, r.Aux == 0 ? TypeFlags.Const : TypeFlags.None, target.Index, 0));
            case RvalueKind.Len:
            case RvalueKind.SizeOf:
            case RvalueKind.AlignOf:
            case RvalueKind.OffsetOf:
                return i32;
            case RvalueKind.HasPayload:
                return boolean;
            case RvalueKind.Slice:
                TypeId sliced = OfPlace(module, body, r.Place);
                if (sliced.IsNone)
                {
                    return TypeId.None;
                }

                TypeNode slicedNode = module.Types[sliced.Index];
                return slicedNode.Kind is TypeKind.Array or TypeKind.Slice
                    ? module.Type(new TypeNode(TypeKind.Slice, slicedNode.Kind == TypeKind.Slice ? slicedNode.Flags & (TypeFlags.Const | TypeFlags.Text) : TypeFlags.None, slicedNode.A, 0))
                    : TypeId.None;
            case RvalueKind.Alloca:
                TypeFlags many = TypeFlags.Nullable | TypeFlags.Many;
                return module.Type(new TypeNode(TypeKind.Ptr, many, r.Type.Index, 0));
            case RvalueKind.OptionalSome:
                TypeId payload = OfOperand(module, body, r.A);
                return payload.IsNone ? TypeId.None : module.Type(new TypeNode(TypeKind.Optional, TypeFlags.None, payload.Index, 0));
            case RvalueKind.OptionalNone:
                return module.Type(new TypeNode(TypeKind.Optional, TypeFlags.None, r.Type.Index, 0));
            case RvalueKind.ResultOk:
            case RvalueKind.ResultErr:
                return r.Type;
            case RvalueKind.Err:
                TypeId failing = OfPlace(module, body, r.Place);
                return failing.IsNone || module.Types[failing.Index].Kind != TypeKind.Result ? TypeId.None : new TypeId(module.Types[failing.Index].B);
            case RvalueKind.Errcode:
                return module.Type(new TypeNode(TypeKind.U16, TypeFlags.None, 0, 0));
            case RvalueKind.Intrinsic:
                if (module.Strings[r.Aux] == ErrorReportIntrinsic)
                {
                    return module.Type(new TypeNode(TypeKind.Void, TypeFlags.None, 0, 0));
                }

                switch (module.Strings[r.Aux])
                {
                    case ErrorNameIntrinsic:
                        return module.Type(new TypeNode(TypeKind.Slice, TypeFlags.Const, module.Type(new TypeNode(TypeKind.U8, TypeFlags.None, 0, 0)).Index, 0));
                    case TextEqlIntrinsic:
                        return module.Type(new TypeNode(TypeKind.Bool, TypeFlags.None, 0, 0));
                    case Utf8NextIntrinsic:
                        return module.Type(new TypeNode(TypeKind.I32, TypeFlags.None, 0, 0));
                    default:
                        return TypeId.None;
                }
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
