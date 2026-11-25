namespace CDelta.Ir;

public readonly record struct TypeNode(TypeKind Kind, TypeFlags Flags, int A, int B);
