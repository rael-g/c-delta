namespace CDelta.Ir;

public readonly record struct SigNode(CallConv Conv, TypeListId Params, TypeId Return, ParamMode ReturnMode, bool Variadic);
