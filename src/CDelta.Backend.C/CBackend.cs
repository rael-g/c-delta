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

        if (module.Target is { NativeInt128: false } && UsesInt128(module))
        {
            text.Append('\n').Append(CPrelude.EmulatedInt128);
        }

        var structs = new CStructWriter(module, names);
        List<StructId> structOrder = structs.DependencyOrder();
        if (structOrder.Count > 0)
        {
            text.Append('\n').Append(structs.ForwardDeclarations(structOrder));
        }

        var enums = new StringBuilder();
        foreach (EnumId id in IrPrinter.OrderedEnums(module))
        {
            EnumDecl declaration = module.Enums[id.Index];
            enums.Append("typedef ").Append(CTypes.Spell(module, names, declaration.Underlying)).Append(' ').Append(names.Enum(id)).Append(";\n");
        }

        if (enums.Length > 0)
        {
            text.Append('\n').Append(enums);
        }

        var emitted = new HashSet<string>();
        var slices = new StringBuilder();
        foreach (TypeNode node in module.Types)
        {
            if (node.Kind == TypeKind.Slice && emitted.Add(CTypes.SliceName(module, names, node)))
            {
                slices.Append(CTypes.SliceDefinition(module, names, node));
            }
        }

        if (slices.Length > 0)
        {
            text.Append('\n').Append(slices);
        }

        List<(int ArrayType, int Struct)> typeOrder = structs.TypeOrder(structOrder);
        if (typeOrder.Count > 0)
        {
            text.Append('\n').Append(structs.Definitions(typeOrder)).Append(structs.Assertions(structOrder));
        }

        var globals = new StringBuilder();
        foreach (GlobalId id in IrPrinter.OrderedGlobals(module))
        {
            globals.Append(GlobalText(module, names, id));
        }

        text.Append('\n').Append(prototypes);
        if (globals.Length > 0)
        {
            text.Append('\n').Append(globals);
        }

        text.Append(definitions);
        string? libc = state.UsesPanic && options.LinkLibC ? CPrelude.LibcUnit : null;
        return new CBackendOutput(text.ToString(), libc);
    }

    private static bool UsesInt128(Module module)
    {
        foreach (TypeNode node in module.Types)
        {
            if (node.Kind is TypeKind.I128 or TypeKind.U128)
            {
                return true;
            }
        }

        return false;
    }

    private static string GlobalText(Module module, CNames names, GlobalId id)
    {
        GlobalDecl global = module.Globals[id.Index];
        string type = CTypes.Spell(module, names, global.Type);
        string name = names.Global(id);
        if (global.Linkage == LinkageKind.Extern)
        {
            return "extern " + type + " " + name + ";\n";
        }

        string storage = (global.Linkage == LinkageKind.Internal ? "static " : string.Empty)
            + (global.ThreadLocal ? "_Thread_local " : string.Empty);
        string init = global.Initializer.IsNone ? string.Empty : " = " + CConstants.Initializer(module, names, global.Initializer);
        return storage + type + " " + name + init + ";\n";
    }
}
