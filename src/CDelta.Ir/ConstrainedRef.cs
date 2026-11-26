namespace CDelta.Ir;

public readonly record struct ConstrainedRef(StringId Param, TypeId Interface, StringId Method);
