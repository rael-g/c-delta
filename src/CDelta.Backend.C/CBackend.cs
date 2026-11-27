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
            if (module.Functions[function.Index].Origin.Kind != OriginKind.CHeader)
            {
                prototypes.Append(CFunctionWriter.Signature(module, function, names, false, null)).Append(";\n");
            }

            if (module.Bodies[function.Index] is not null)
            {
                definitions.Append('\n').Append(new CFunctionWriter(module, function, names, options, state).Write());
            }
        }

        FunctionId? handler = PanicHandler(module);
        var text = new StringBuilder(ImportLines(module));
        text.Append(CPrelude.Includes);
        if (state.UsesUnusedMarker)
        {
            text.Append('\n').Append(CPrelude.UnusedMarker);
        }

        if (state.UsesAlloca)
        {
            text.Append('\n').Append(CPrelude.Alloca);
        }

        if (state.UsesReport && options.LinkLibC)
        {
            state.UsesTrace = true;
        }

        if (state.UsesTrace)
        {
            bool printed = options.LinkLibC && (state.UsesPanic && handler is null || state.UsesReport);
            text.Append('\n').Append(CPrelude.TraceCounter);
            if (state.UsesTracePush || printed)
            {
                text.Append(CPrelude.TraceRing);
            }

            if (state.UsesTracePush)
            {
                text.Append(CPrelude.TracePush);
            }
        }

        bool defaultPanic = state.UsesPanic && handler is null;
        bool report = state.UsesReport && options.LinkLibC;
        if (options.LinkLibC && (defaultPanic || report))
        {
            text.Append('\n').Append(CPrelude.LibcProtocol).Append('\n').Append(CPrelude.LibcWriters);
            if (state.UsesTrace)
            {
                text.Append(CPrelude.TracePrint);
            }
        }

        if (report)
        {
            text.Append('\n').Append(CPrelude.ReportError);
        }

        if (defaultPanic)
        {
            if (options.LinkLibC)
            {
                string withLibc = CPrelude.PanicWithLibc
                    .Replace("    @@TRACE_CALL@@\n", state.UsesTrace ? "    cd_trace_print();\n" : string.Empty);
                text.Append('\n').Append(withLibc);
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

        if (state.FloatCasts.Count > 0)
        {
            text.Append('\n').Append(FloatCastHelpers(module, state));
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
            if (CNames.IsImported(module, declaration.Origin))
            {
                continue;
            }

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

        bool usesDyn = module.Types.Any(node => node.Kind == TypeKind.Dyn);
        bool usesClosure = module.Types.Any(node => node.Kind == TypeKind.Closure);
        if (usesDyn || usesClosure)
        {
            text.Append('\n').Append(CPrelude.FnType);
        }

        if (usesDyn)
        {
            text.Append(CPrelude.DynType);
        }

        if (usesClosure)
        {
            text.Append(CPrelude.ClosureType);
        }

        if (state.UsesTextEql)
        {
            text.Append('\n').Append(CPrelude.TextEql);
        }

        if (state.UsesUtf8Next)
        {
            text.Append('\n').Append(CPrelude.Utf8Next);
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
        if (module.Impls.Count > 0)
        {
            text.Append('\n').Append(DispatchTables(module, names));
        }

        if (state.UsesPanic && handler is { } panicHandler)
        {
            text.Append('\n').Append(PanicDispatch(module, names, panicHandler));
        }

        if (globals.Length > 0)
        {
            text.Append('\n').Append(globals);
        }

        text.Append(definitions);
        string? libc = (defaultPanic || report) && options.LinkLibC ? CPrelude.LibcUnit : null;
        return new CBackendOutput(text.ToString(), libc);
    }

    public static string TableName(int impl) => "cd_vtable_" + impl;

    private static string DispatchTables(Module module, CNames names)
    {
        var text = new StringBuilder();
        for (int index = 0; index < module.Impls.Count; index++)
        {
            ImplDecl impl = module.Impls[index];
            InterfaceDecl contract = module.Interfaces[module.Types[impl.Interface.Index].A];
            var entries = new List<string>();
            for (int slot = 0; slot < contract.Methods.Count; slot++)
            {
                InterfaceMethod method = module.InterfaceMethods[contract.Methods.Start + slot];
                string entry = "0";
                for (int i = 0; i < impl.Methods.Count && !method.IsStatic; i++)
                {
                    ImplMethod candidate = module.ImplMethods[impl.Methods.Start + i];
                    if (candidate.Name == method.Name)
                    {
                        entry = "(cd_fn)" + names.Function(candidate.Function);
                    }
                }

                entries.Add(entry);
            }

            text.Append("static const cd_fn ").Append(TableName(index)).Append("[] = { ").Append(string.Join(", ", entries)).Append(" };\n");
        }

        return text.ToString();
    }

    private static string ErrorHelpers(Module module, CNames names, CEmitState state)
    {
        var text = new StringBuilder();
        foreach (int index in state.ErrorFromEnums.Union(state.ErrorToEnums))
        {
            EnumDecl declaration = module.Enums[index];
            string type = names.Enum(new EnumId(index));
            string suffix = CNames.MangleSpelling(type);
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
                text.Append("static uint16_t cd_err_from_").Append(suffix).Append('(').Append(type).Append(" value)\n{\n    switch (value)\n    {\n");
                text.Append(forward).Append("    default: return 0;\n    }\n}\n\n");
            }

            if (state.ErrorToEnums.Contains(index))
            {
                text.Append("static ").Append(type).Append(" cd_err_to_").Append(suffix).Append("(uint16_t code)\n{\n    switch (code)\n    {\n");
                text.Append(reverse).Append("    default: return (").Append(type).Append(")0;\n    }\n}\n\n");
            }
        }

        return text.ToString();
    }

    private static string FloatCastHelpers(Module module, CEmitState state)
    {
        Target target = module.Target!.Value;
        var text = new StringBuilder();
        foreach ((string from, string to, bool trap) in state.FloatCasts)
        {
            TypeKind toKind = IrTypesByName(to);
            int bits = IrTypes.BitWidth(toKind, target);
            bool signed = IrTypes.IsSigned(toKind, target);
            string sourceC = CTypes.Primitive(IrTypesByName(from));
            string targetC = CTypes.Primitive(toKind);
            string high = (System.Numerics.BigInteger.One << (signed ? bits - 1 : bits)).ToString() + ".0";
            string low = signed ? "-" + high : "0.0";
            if (trap)
            {
                string lowerBound = signed && bits < 64 ? "v > (" + low + " - 1.0)" : signed ? "v >= " + low : "v > -1.0";
                text.Append("static inline ").Append(targetC).Append(" cd_f2i_trap_").Append(from).Append('_').Append(to).Append('(').Append(sourceC)
                    .Append(" v, const char* file, int32_t line)\n{\n    if (!(").Append(lowerBound).Append(" && v < ").Append(high)
                    .Append("))\n    {\n        cd_panic((const uint8_t*)\"float cast out of range\", 23, file, line);\n    }\n    return (").Append(targetC).Append(")v;\n}\n\n");
            }
            else
            {
                text.Append("static inline ").Append(targetC).Append(" cd_f2i_wrap_").Append(from).Append('_').Append(to).Append('(').Append(sourceC)
                    .Append(" v)\n{\n    if (v != v)\n    {\n        return 0;\n    }\n    if (v <= ").Append(low).Append(")\n    {\n        return ")
                    .Append(signed ? MinLiteral(bits) : "0").Append(";\n    }\n    if (v >= ").Append(high).Append(")\n    {\n        return ")
                    .Append(MaxLiteral(bits, signed, targetC)).Append(";\n    }\n    return (").Append(targetC).Append(")v;\n}\n\n");
            }

        }

        return text.ToString();
    }

    private static string MinLiteral(int bits) => bits == 64 ? "(-9223372036854775807LL - 1)" : "(" + (-(System.Numerics.BigInteger.One << (bits - 1))) + ")";

    private static string MaxLiteral(int bits, bool signed, string targetC)
    {
        System.Numerics.BigInteger max = (System.Numerics.BigInteger.One << (signed ? bits - 1 : bits)) - 1;
        return "((" + targetC + ")" + max + (bits == 64 ? (signed ? "LL" : "ULL") : "u") + ")";
    }

    private static TypeKind IrTypesByName(string name) =>
        Enum.GetValues<TypeKind>().First(kind => kind <= TypeKind.CULongLong && IrTypes.Name(kind) == name);

    private static string ImportLines(Module module)
    {
        var text = new StringBuilder();
        foreach (CImportDecl import in module.CImports)
        {
            foreach (string define in import.Defines)
            {
                int equals = define.IndexOf('=');
                text.Append("#define ").Append(equals < 0 ? define + " 1" : define[..equals] + " " + define[(equals + 1)..]).Append('\n');
            }
        }

        foreach (CImportDecl import in module.CImports)
        {
            foreach (string header in import.Headers)
            {
                text.Append("#include \"").Append(header).Append("\"\n");
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
