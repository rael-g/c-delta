namespace CDelta.Ir;

public readonly record struct InterfaceMethod(StringId Name, bool IsStatic, SigId Sig, FunctionId Default);
