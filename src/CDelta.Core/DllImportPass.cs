using CDelta.Diagnostics;
using CDelta.Ir;

namespace CDelta.Core;

public static class DllImportPass
{
    public static void Run(Module module, DiagnosticBag diagnostics)
    {
        var firstByName = new Dictionary<string, FunctionId>();
        for (int i = 0; i < module.Functions.Count; i++)
        {
            FunctionDecl function = module.Functions[i];
            if (function.Origin.Kind != OriginKind.CLibrary)
            {
                continue;
            }

            string name = module.Text(function.Origin.CName);
            if (!firstByName.TryGetValue(name, out FunctionId first))
            {
                firstByName.Add(name, new FunctionId(i));
                continue;
            }

            if (!SameSignature(module, module.Functions[first.Index], function))
            {
                string path = function.Span.IsNone ? module.Name : module.Sources[function.Span.File.Index].FullPath;
                diagnostics.Add(
                    DiagnosticCode.CD6010,
                    new Location(path, function.Span.Line, function.Span.Column),
                    new Note(null, "the C function " + name + " is declared twice with different signatures"));
            }
        }
    }

    private static bool SameSignature(Module module, FunctionDecl a, FunctionDecl b)
    {
        if (a.Return != b.Return || a.Variadic != b.Variadic || a.Conv != b.Conv || a.Params.Count != b.Params.Count)
        {
            return false;
        }

        for (int i = 0; i < a.Params.Count; i++)
        {
            ParamDecl left = module.Params[a.Params.Start + i];
            ParamDecl right = module.Params[b.Params.Start + i];
            if (left.Type != right.Type || left.Mode != right.Mode)
            {
                return false;
            }
        }

        return true;
    }
}
