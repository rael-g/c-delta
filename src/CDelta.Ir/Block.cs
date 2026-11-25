namespace CDelta.Ir;

public readonly record struct Block(List<StatementId> Statements, Terminator? Terminator);
