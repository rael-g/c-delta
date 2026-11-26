namespace CDelta.Ir;

public readonly record struct ImplDecl(StringId Key, TypeId Type, TypeId Interface, IdRange Methods);
