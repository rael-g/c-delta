namespace CDelta.Ir;

public readonly record struct SwitchCase(ConstId Value, BlockId Target);
