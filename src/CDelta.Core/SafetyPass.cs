using CDelta.Diagnostics;
using CDelta.Ir;

namespace CDelta.Core;

public static class SafetyPass
{
    public static void Run(Module module, CoreOptions options, DiagnosticBag diagnostics)
    {
        for (int i = 0; i < module.Functions.Count; i++)
        {
            Body? body = module.Bodies[i];
            if (body is null)
            {
                continue;
            }

            bool checksOn = module.Functions[i].Safety switch
            {
                SafetyOverride.On => true,
                SafetyOverride.Off => false,
                _ => options.Mode is BuildMode.Debug or BuildMode.ReleaseSafe,
            };
            new FunctionSafety(module, body, options.Target, checksOn).Run();
            if (!options.TracesErrors)
            {
                RemoveTraces(module, body);
            }
        }

        LayoutPass.Run(module, options.Target, diagnostics);
        module.Stage = ModuleStage.Final;
        module.Target = options.Target;
    }

    private static bool IsTracePrint(Module module, Body body, Statement statement) =>
        statement.Kind == StatementKind.Intrinsic && module.Strings[body.Rvalues[statement.Rvalue.Index].Aux] == IrTyper.TracePrintIntrinsic;

    private static void RemoveTraces(Module module, Body body)
    {
        foreach (Block block in body.Blocks)
        {
            block.Statements.RemoveAll(id => body.Statements[id.Index].Kind is StatementKind.TracePush or StatementKind.TraceSave or StatementKind.TraceRestore
                || IsTracePrint(module, body, body.Statements[id.Index]));
        }
    }
}
