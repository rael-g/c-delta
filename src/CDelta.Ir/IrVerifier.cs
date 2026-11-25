using CDelta.Diagnostics;

namespace CDelta.Ir;

public static class IrVerifier
{
    public static DiagnosticBag Verify(Module module, ModuleStage stage)
    {
        var bag = new DiagnosticBag();
        if (module.Stage != stage)
        {
            Report(bag, module, SourceSpan.None, module.Name, "module stage is " + IrNames.Stage(module.Stage) + ", expected " + IrNames.Stage(stage));
        }

        for (int i = 0; i < module.Structs.Count; i++)
        {
            StructDecl declaration = module.Structs[i];
            if (stage == ModuleStage.Final && declaration.Size == StructDecl.NotComputed)
            {
                Report(bag, module, declaration.Span, module.Text(declaration.Key), "a struct has no layout in the final stage");
            }
        }

        for (int i = 0; i < module.Functions.Count; i++)
        {
            new FunctionVerifier(module, stage, new FunctionId(i), bag).Run();
        }

        return bag;
    }

    internal static void Report(DiagnosticBag bag, Module module, SourceSpan span, string function, string detail)
    {
        Location location = span.IsNone || span.File.Index >= module.Sources.Count
            ? new Location(module.Name, 0, 0)
            : new Location(module.Sources[span.File.Index].FullPath, span.Line, span.Column);
        bag.Add(DiagnosticCode.CD9002, location, new Note(null, function + ": " + detail));
    }
}
