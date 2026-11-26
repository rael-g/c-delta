namespace CDelta.Ir;

public readonly record struct ImplMethod(StringId Name, FunctionId Function, TypeListId TypeArgs);
