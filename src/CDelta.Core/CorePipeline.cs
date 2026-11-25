using CDelta.Diagnostics;
using CDelta.Ir;

namespace CDelta.Core;

public static class CorePipeline
{
    public static Module? Run(Module lowered, IBodyProvider provider, CoreOptions options, DiagnosticBag diagnostics)
    {
        List<FunctionId> roots = RootsPass.Find(lowered);
        Module instantiated = new Instantiator(lowered, provider, diagnostics).Run(roots);
        DllImportPass.Run(instantiated, diagnostics);
        if (diagnostics.HasError)
        {
            return null;
        }

        EntryPointPass.Run(instantiated);
        SafetyPass.Run(instantiated, options);
        DiagnosticBag verification = IrVerifier.Verify(instantiated, ModuleStage.Final);
        foreach (CDelta.Diagnostics.Diagnostic item in verification.Items)
        {
            diagnostics.Add(item);
        }

        return diagnostics.HasError ? null : instantiated;
    }
}
