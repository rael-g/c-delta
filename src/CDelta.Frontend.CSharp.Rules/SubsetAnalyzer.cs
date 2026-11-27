using System.Collections.Immutable;
using DiagnosticCode = CDelta.Diagnostics.DiagnosticCode;
using CDelta.Frontend.CSharp.Rules.Checks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace CDelta.Frontend.CSharp.Rules;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SubsetAnalyzer : DiagnosticAnalyzer
{
    private static readonly SubsetRule[] _rules =
    [
        new AllowlistRule(),
        new ClassRule(),
        new KindRule(DiagnosticCode.CD1002, SyntaxKind.RecordDeclaration, SyntaxKind.RecordStructDeclaration),
        new InheritanceRule(),
        new PropertyRule(),
        new IndexerRule(),
        new KindRule(DiagnosticCode.CD1006, SyntaxKind.EventDeclaration, SyntaxKind.EventFieldDeclaration),
        new OperatorRule(),
        new KindRule(DiagnosticCode.CD1008, SyntaxKind.DestructorDeclaration),
        new StaticConstructorRule(),
        new KindRule(DiagnosticCode.CD1010, SyntaxKind.TryStatement),
        new ThrowRule(),
        new AsyncRule(),
        new KindRule(DiagnosticCode.CD1013, SyntaxKind.YieldReturnStatement, SyntaxKind.YieldBreakStatement),
        new KindRule(DiagnosticCode.CD1014, SyntaxKind.QueryExpression),
        new KindRule(DiagnosticCode.CD1015, SyntaxKind.LockStatement),
        new DynamicRule(),
        new ObjectRule(),
        new KindRule(DiagnosticCode.CD1018, SyntaxKind.AnonymousObjectCreationExpression),
        new ArrayRule(),
        new CharRule(),
        new StringPointerRule(),
        new StringPositionRule(),
        new ConstraintRule(),
        new StringConcatenationRule(),
        new DirectiveRule(),
        new DeconstructRule(),
        new FieldVisibilityRule(),
        new KindRule(DiagnosticCode.CD1026, SyntaxKind.AsExpression),
        new TypedReferenceRule(),
        new ParamsArrayRule(),
        new DelegateCombinationRule(),
        new SwitchExhaustivenessRule(),
        new OpaqueRule(),
        new BitsRule(),
        new ThreadStaticRule(),
        new StackEscapeRule(),
        new LoopCaptureRule(),
        new TopLevelArgsRule(),
        new CapturingEscapeRule(),
        new ErrorRule(),
        new CInteropRule(),
        new SurrogateRule(),
    ];

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        [.. Descriptors.All];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        foreach (SubsetRule rule in _rules)
        {
            rule.Register(context);
        }
    }
}
