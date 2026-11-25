namespace CDelta.Ir;

public readonly record struct ParamDecl(StringId Name, TypeId Type, ParamMode Mode);
