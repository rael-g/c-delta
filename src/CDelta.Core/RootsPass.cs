using CDelta.Ir;

namespace CDelta.Core;

public static class RootsPass
{
    public const string EntryPointAttribute = "cdelta.EntryPoint";

    public const string PanicHandlerAttribute = "cdelta.PanicHandler";

    private static readonly string[] _rootAttributes = [EntryPointAttribute, PanicHandlerAttribute];

    public static List<FunctionId> Find(Module module)
    {
        var roots = new List<FunctionId>();
        for (int i = 0; i < module.Functions.Count; i++)
        {
            if (HasAnyRootAttribute(module, module.Functions[i]))
            {
                roots.Add(new FunctionId(i));
            }
        }

        return roots;
    }

    public static bool HasAttribute(Module module, FunctionDecl function, string name)
    {
        for (int i = 0; i < function.Attributes.Count; i++)
        {
            if (module.Text(module.Attributes[function.Attributes.Start + i].Name) == name)
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasAnyRootAttribute(Module module, FunctionDecl function)
    {
        foreach (string name in _rootAttributes)
        {
            if (HasAttribute(module, function, name))
            {
                return true;
            }
        }

        return false;
    }
}
