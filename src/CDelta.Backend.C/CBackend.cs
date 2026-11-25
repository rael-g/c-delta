using System.Text;
using CDelta.Ir;

namespace CDelta.Backend.C;

public static class CBackend
{
    public static CBackendOutput Generate(Module module, BackendOptions options)
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
        if (state.UsesPanic)
        {
            if (options.LinkLibC)
            {
                text.Append('\n').Append(CPrelude.LibcProtocol).Append('\n').Append(CPrelude.PanicWithLibc);
            }
            else
            {
                text.Append('\n').Append(CPrelude.PanicWithoutLibc);
            }
        }

        text.Append('\n').Append(prototypes).Append(definitions);
        string? libc = state.UsesPanic && options.LinkLibC ? CPrelude.LibcUnit : null;
        return new CBackendOutput(text.ToString(), libc);
    }
}
