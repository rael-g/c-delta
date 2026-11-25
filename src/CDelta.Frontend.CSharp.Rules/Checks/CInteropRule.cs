using DiagnosticCode = CDelta.Diagnostics.DiagnosticCode;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace CDelta.Frontend.CSharp.Rules.Checks;

internal sealed class CInteropRule : SubsetRule
{
    private static readonly string[] _rejectedDllImportOptions = ["CharSet", "SetLastError", "BestFitMapping", "ThrowOnUnmappableChar"];

    public override DiagnosticCode Code => DiagnosticCode.CD6002;

    public override void Register(AnalysisContext context)
    {
        context.RegisterSyntaxNodeAction(CheckImportClass, SyntaxKind.ClassDeclaration);
        context.RegisterSyntaxNodeAction(CheckMethod, SyntaxKind.MethodDeclaration);
        context.RegisterOperationAction(CheckUse, OperationKind.Invocation, OperationKind.FieldReference);
    }

    private void CheckImportClass(SyntaxNodeAnalysisContext context)
    {
        var declaration = (ClassDeclarationSyntax)context.Node;
        if (context.SemanticModel.GetDeclaredSymbol(declaration) is not { } symbol || !Syntax.HasAttribute(symbol, "CDelta.ImportAttribute"))
        {
            return;
        }

        if (!Syntax.Has(declaration, SyntaxKind.StaticKeyword) || !Syntax.Has(declaration, SyntaxKind.PartialKeyword))
        {
            context.ReportDiagnostic(Create(DiagnosticCode.CD6002, declaration.Identifier.GetLocation()));
        }
    }

    private void CheckMethod(SyntaxNodeAnalysisContext context)
    {
        var declaration = (MethodDeclarationSyntax)context.Node;
        if (context.SemanticModel.GetDeclaredSymbol(declaration) is not { } method)
        {
            return;
        }

        if (Syntax.HasAttribute(method, "CDelta.CMacroAttribute") && !(method.IsStatic && method.IsExtern))
        {
            context.ReportDiagnostic(Create(DiagnosticCode.CD6008, declaration.Identifier.GetLocation()));
        }

        foreach (AttributeSyntax attribute in declaration.AttributeLists.SelectMany(l => l.Attributes))
        {
            if (!IsDllImport(attribute))
            {
                continue;
            }

            foreach (AttributeArgumentSyntax argument in attribute.ArgumentList?.Arguments ?? [])
            {
                string? name = argument.NameEquals?.Name.Identifier.ValueText;
                bool rejected = name is not null && _rejectedDllImportOptions.Contains(name)
                    || name == "PreserveSig" && argument.Expression.IsKind(SyntaxKind.FalseLiteralExpression);
                if (rejected)
                {
                    context.ReportDiagnostic(Create(DiagnosticCode.CD6007, argument.GetLocation()));
                }
            }
        }
    }

    private static bool IsDllImport(AttributeSyntax attribute) =>
        attribute.Name.ToString() is "DllImport" or "DllImportAttribute" or "System.Runtime.InteropServices.DllImport";

    private void CheckUse(OperationAnalysisContext context)
    {
        ISymbol? used = context.Operation switch
        {
            IInvocationOperation invocation => invocation.TargetMethod,
            IFieldReferenceOperation field => field.Field,
            _ => null,
        };
        if (used is not null && Syntax.HasAttribute(used, "CDelta.UnsupportedAttribute"))
        {
            context.ReportDiagnostic(Create(DiagnosticCode.CD6003, context.Operation.Syntax.GetLocation()));
        }
    }

    private static Microsoft.CodeAnalysis.Diagnostic Create(DiagnosticCode code, Location location) =>
        Microsoft.CodeAnalysis.Diagnostic.Create(Descriptors.For(code), location);
}
