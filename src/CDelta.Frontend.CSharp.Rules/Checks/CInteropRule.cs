using DiagnosticCode = CDelta.Diagnostics.DiagnosticCode;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using System.Collections.Concurrent;

namespace CDelta.Frontend.CSharp.Rules.Checks;

internal sealed class CInteropRule : SubsetRule
{
    private const string ExportAttributeName = "CDelta.ExportAttribute";

    private static readonly string[] _rejectedDllImportOptions = ["CharSet", "SetLastError", "BestFitMapping", "ThrowOnUnmappableChar"];

    public override DiagnosticCode Code => DiagnosticCode.CD6002;

    public override void Register(AnalysisContext context)
    {
        context.RegisterCompilationStartAction(compilation =>
        {
            var names = new ConcurrentDictionary<string, ConcurrentBag<Location>>();
            compilation.RegisterSyntaxNodeAction(node => CollectExport(node, names), SyntaxKind.MethodDeclaration);
            compilation.RegisterCompilationEndAction(end =>
            {
                foreach (ConcurrentBag<Location> places in names.Values)
                {
                    Location[] ordered = [.. places.OrderBy(place => place.SourceTree?.FilePath, StringComparer.Ordinal).ThenBy(place => place.SourceSpan.Start)];
                    foreach (Location place in ordered.Skip(1))
                    {
                        end.ReportDiagnostic(Create(DiagnosticCode.CD6005, place));
                    }
                }
            });
        });
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

        CheckExport(context, declaration, method);
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

    private static AttributeData? ExportOf(ISymbol symbol) =>
        symbol.GetAttributes().FirstOrDefault(attribute => attribute.AttributeClass?.ToDisplayString() == ExportAttributeName);

    private static void CollectExport(SyntaxNodeAnalysisContext context, ConcurrentDictionary<string, ConcurrentBag<Location>> names)
    {
        var declaration = (MethodDeclarationSyntax)context.Node;
        if (context.SemanticModel.GetDeclaredSymbol(declaration) is not { } method || ExportOf(method) is not { } export)
        {
            return;
        }

        string name = export.ConstructorArguments.Length == 1 && export.ConstructorArguments[0].Value is string given ? given : method.Name;
        names.GetOrAdd(name, _ => []).Add(declaration.Identifier.GetLocation());
    }

    private static void CheckExport(SyntaxNodeAnalysisContext context, MethodDeclarationSyntax declaration, IMethodSymbol method)
    {
        if (ExportOf(method) is null)
        {
            return;
        }

        if (method.IsGenericMethod || method.IsVararg)
        {
            context.ReportDiagnostic(Create(DiagnosticCode.CD6006, declaration.Identifier.GetLocation()));
            return;
        }

        if (!IsCType(method.ReturnType) || method.Parameters.Any(parameter => !IsCType(parameter.Type)))
        {
            context.ReportDiagnostic(Create(DiagnosticCode.CD6004, declaration.Identifier.GetLocation()));
        }
    }

    private static bool IsCType(ITypeSymbol type) => type switch
    {
        IPointerTypeSymbol => true,
        IFunctionPointerTypeSymbol function => IsCType(function.Signature.ReturnType) && function.Signature.Parameters.All(parameter => IsCType(parameter.Type)),
        { SpecialType: SpecialType.System_Void or SpecialType.System_Boolean or SpecialType.System_SByte or SpecialType.System_Byte
            or SpecialType.System_Int16 or SpecialType.System_UInt16 or SpecialType.System_Int32 or SpecialType.System_UInt32
            or SpecialType.System_Int64 or SpecialType.System_UInt64 or SpecialType.System_Single or SpecialType.System_Double
            or SpecialType.System_IntPtr or SpecialType.System_UIntPtr } => true,
        INamedTypeSymbol { Name: "CLong" or "CULong", ContainingNamespace: { Name: "InteropServices" } } => true,
        INamedTypeSymbol { TypeKind: Microsoft.CodeAnalysis.TypeKind.Struct or Microsoft.CodeAnalysis.TypeKind.Enum } named => ExportOf(named) is not null,
        _ => false,
    };

    private static Microsoft.CodeAnalysis.Diagnostic Create(DiagnosticCode code, Location location) =>
        Microsoft.CodeAnalysis.Diagnostic.Create(Descriptors.For(code), location);
}
