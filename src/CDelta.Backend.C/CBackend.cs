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

        FunctionId? handler = PanicHandler(module);
        var text = new StringBuilder(CPrelude.Includes);
        if (state.UsesUnusedMarker)
        {
            text.Append('\n').Append(CPrelude.UnusedMarker);
        }

        if (state.UsesAlloca)
        {
            text.Append('\n').Append(CPrelude.Alloca);
        }

        if (state.UsesTrace)
        {
            text.Append('\n').Append(CPrelude.TraceRing);
        }

        if (state.UsesPanic && handler is null)
        {
            if (options.LinkLibC)
            {
                string withLibc = CPrelude.PanicWithLibc
                    .Replace("@@TRACE_FUNCTION@@\n", state.UsesTrace ? CPrelude.TracePrint : string.Empty)
                    .Replace("    @@TRACE_CALL@@\n", state.UsesTrace ? "    cd_trace_print();\n" : string.Empty);
                text.Append('\n').Append(CPrelude.LibcProtocol).Append('\n').Append(withLibc);
            }
            else
            {
                text.Append('\n').Append(CPrelude.PanicWithoutLibc);
            }

            text.Append('\n').Append(CPrelude.PanicDirect);
        }
        else if (state.UsesPanic)
        {
            text.Append('\n').Append(CPrelude.PanicPrototype);
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

        if (state.ErrorFromEnums.Count + state.ErrorToEnums.Count > 0)
        {
            text.Append('\n').Append(ErrorHelpers(module, names, state));
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
        if (state.UsesPanic && handler is { } panicHandler)
        {
            text.Append('\n').Append(PanicDispatch(module, names, panicHandler));
        }

        if (globals.Length > 0)
        {
            text.Append('\n').Append(globals);
        }

        text.Append(definitions);
        string? libc = state.UsesPanic && handler is null && options.LinkLibC ? CPrelude.LibcUnit : null;
        return new CBackendOutput(text.ToString(), libc);
    }

    private static string ErrorHelpers(Module module, CNames names, CEmitState state)
    {
        var text = new StringBuilder();
        foreach (int index in state.ErrorFromEnums.Union(state.ErrorToEnums))
        {
            EnumDecl declaration = module.Enums[index];
            string type = names.Enum(new EnumId(index));
            var forward = new StringBuilder();
            var reverse = new StringBuilder();
            for (int i = 0; i < declaration.Members.Count; i++)
            {
                EnumMember member = module.EnumMembers[declaration.Members.Start + i];
                int code = module.ErrorNames[module.Text(member.Name)];
                string literal = CConstants.EnumLiteral(module, declaration, member.Value, type);
                forward.Append("    case ").Append(literal).Append(": return ").Append(code).Append(";\n");
                reverse.Append("    case ").Append(code).Append(": return ").Append(literal).Append(";\n");
            }

            if (state.ErrorFromEnums.Contains(index))
            {
                text.Append("static uint16_t cd_err_from_").Append(type).Append('(').Append(type).Append(" value)\n{\n    switch (value)\n    {\n");
                text.Append(forward).Append("    default: return 0;\n    }\n}\n\n");
            }

            if (state.ErrorToEnums.Contains(index))
            {
                text.Append("static ").Append(type).Append(" cd_err_to_").Append(type).Append("(uint16_t code)\n{\n    switch (code)\n    {\n");
                text.Append(reverse).Append("    default: return (").Append(type).Append(")0;\n    }\n}\n\n");
            }
        }

        return text.ToString();
    }

    private const string PanicHandlerAttribute = "cdelta.PanicHandler";

    private static FunctionId? PanicHandler(Module module)
    {
        for (int i = 0; i < module.Functions.Count; i++)
        {
            FunctionDecl function = module.Functions[i];
            for (int a = 0; a < function.Attributes.Count; a++)
            {
                if (module.Text(module.Attributes[function.Attributes.Start + a].Name) == PanicHandlerAttribute)
                {
                    return new FunctionId(i);
                }
            }
        }

        return null;
    }

    private static string PanicDispatch(Module module, CNames names, FunctionId handler)
    {
        FunctionDecl function = module.Functions[handler.Index];
        string slice = CTypes.Spell(module, names, module.Params[function.Params.Start].Type);
        return "static noreturn void cd_panic(const uint8_t* msg, int32_t msg_len, const char* file, int32_t line)\n{\n"
            + "    " + slice + " message = { msg, msg_len };\n"
            + "    " + slice + " path = { (const uint8_t*)file, (int32_t)__builtin_strlen(file) };\n"
            + "    " + names.Function(handler) + "(message, path, line);\n"
            + "    __builtin_trap();\n}\n";
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
