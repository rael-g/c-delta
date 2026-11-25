namespace CDelta.Ir;

public readonly record struct ConstNode(ConstKind Kind, TypeId Type, Int128 Value, BytesId Bytes, TypeListId Items);
