using System.Text;
using CDelta.Ir;

namespace CDelta.Backend.C;

public static class CBackend
{
    public static string Generate(Module module, BackendOptions options)
    {
        var names = new CNames(module);
        var state = new CEmitState();
        List<FunctionId> order = IrPrinter.OrderedFunctions(module);

        var prototypes = new StringBuilder();
        var definitions = new StringBuilder();
        foreach (FunctionId function in order)
        {
            prototypes.Append(CFunctionWriter.Signature(module, function, names, false, null)).Append(";\n");
            if (module.Bodies[function.Index] is not null)
            {
                definitions.Append('\n').Append(new CFunctionWriter(module, function, names, options, state).Write());
            }
        }

        var text = new StringBuilder(CPrelude.Includes);
        if (state.UsesPanic && options.LinkLibC)
        {
            text.Append(CPrelude.LibcIncludes);
        }

        if (state.UsesPanic)
        {
            text.Append('\n').Append(options.LinkLibC ? CPrelude.PanicWithLibc : CPrelude.PanicWithoutLibc);
        }

        text.Append('\n').Append(prototypes).Append(definitions);
        return text.ToString();
    }
}
