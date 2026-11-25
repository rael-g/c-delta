using CDelta.Ir;

namespace CDelta.Core;

public static class SafetyPass
{
    public static void Run(Module module, CoreOptions options)
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
        }

        LayoutPass.Run(module, options.Target);
        module.Stage = ModuleStage.Final;
        module.Target = options.Target;
    }
}
