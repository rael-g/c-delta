namespace CDelta.Ir;

public sealed class Body
{
    public List<LocalDecl> Locals { get; } = [];

    public List<Block> Blocks { get; } = [];

    public List<Statement> Statements { get; } = [];

    public List<Place> Places { get; } = [];

    public List<Projection> Projections { get; } = [];

    public List<Operand> Operands { get; } = [];

    public List<Rvalue> Rvalues { get; } = [];

    public List<OperandId> Args { get; } = [];

    public List<SwitchCase> SwitchCases { get; } = [];
}
