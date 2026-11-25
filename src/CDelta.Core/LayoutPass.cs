using CDelta.Ir;

namespace CDelta.Core;

public static class LayoutPass
{
    public static void Run(Module module, Target target)
    {
        var layouts = new TypeLayouts(module, target);
        for (int i = 0; i < module.Structs.Count; i++)
        {
            layouts.OfStruct(new StructId(i));
        }
    }
}
