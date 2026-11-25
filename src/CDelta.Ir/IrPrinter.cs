using System.Text;
using System.Text.RegularExpressions;

namespace CDelta.Ir;

public static partial class IrPrinter
{
    private const string Indent = "  ";

    private static readonly HashSet<string> _keywords =
    [
        "module", "stage", "lowered", "instantiated", "final", "target", "source", "cimport", "struct", "enum",
        "union", "interface", "opaque", "global", "fn", "impl", "error", "layout", "size", "align", "minsize",
        "origin", "copy", "const", "fnref", "undef", "binop", "unop", "cast", "addr", "aggregate", "len", "tag",
        "haspayload", "errcode", "sizeof", "alignof", "offsetof", "call", "intrinsic", "comptime", "direct",
        "indirect", "closure", "dyn", "constrained", "param", "local", "live", "dead", "check", "goto", "branch",
        "switch", "return", "unreachable", "panic", "default", "trap", "wrap", "assume", "true", "false", "null",
        "bytes", "bits", "nullable", "many", "volatile", "ref", "out", "internal", "export", "extern", "weak",
        "inline", "always", "never", "cc", "safety", "on", "off", "type", "user", "temp", "to", "ptr", "slice",
        "array", "optional", "result", "errorset", "anyerror", "alloca", "zero", "fill", "void", "noreturn", "bool", "isize", "usize",
    ];

    [GeneratedRegex(@"^[A-Za-z_][A-Za-z0-9_]*(\.[A-Za-z_][A-Za-z0-9_]*)*$")]
    private static partial Regex BareReference();

    public static string Print(Module module)
    {
        var text = new StringBuilder();
        text.Append("module ").Append(Reference(module.Name)).Append(" stage ").Append(IrNames.Stage(module.Stage));
        if (module.Target is Target target)
        {
            text.Append(" target ").Append(target.Triple);
        }

        text.Append('\n');
        if (module.Sources.Count > 0)
        {
            text.Append('\n');
            for (int i = 0; i < module.Sources.Count; i++)
            {
                SourceFile file = module.Sources[i];
                text.Append("source ").Append(i + 1).Append(' ').Append(Quote(file.FullPath)).Append(' ')
                    .Append(Quote(file.DisplayPath)).Append('\n');
            }
        }

        for (int i = 0; i < module.CImports.Count; i++)
        {
            PrintCImport(module.CImports[i], i + 1, text);
        }

        foreach (EnumId declaration in OrderedEnums(module))
        {
            text.Append('\n');
            PrintEnum(module, declaration, text);
        }

        foreach (StructId declaration in OrderedStructs(module))
        {
            text.Append('\n');
            PrintStruct(module, declaration, text);
        }

        foreach (OpaqueId opaque in OrderedOpaques(module))
        {
            text.Append('\n');
            OpaqueDecl declaration = module.Opaques[opaque.Index];
            text.Append("opaque ").Append(Reference(module.Text(declaration.Key)));
            PrintOrigin(module, declaration.Origin, text);
            text.Append(' ').Append(Span(declaration.Span)).Append('\n');
        }

        foreach (GlobalId global in OrderedGlobals(module))
        {
            text.Append('\n');
            PrintGlobal(module, global, text);
        }

        if (module.ErrorNames.Count > 0)
        {
            text.Append('\n');
            foreach ((string name, int code) in module.ErrorNames)
            {
                text.Append("error ").Append(name).Append(" = ").Append(code).Append('\n');
            }
        }

        foreach (FunctionId function in OrderedFunctions(module))
        {
            text.Append('\n');
            PrintFunction(module, function, text);
        }

        return text.ToString();
    }

    public static string TypeText(Module module, TypeId type)
    {
        TypeNode node = module.Types[type.Index];
        switch (node.Kind)
        {
            case TypeKind.Ptr:
                StringBuilder pointer = new StringBuilder("ptr<").Append(TypeText(module, new TypeId(node.A))).Append('>');
                AppendFlag(pointer, node.Flags, TypeFlags.Nullable, "nullable");
                AppendFlag(pointer, node.Flags, TypeFlags.Const, "const");
                AppendFlag(pointer, node.Flags, TypeFlags.Volatile, "volatile");
                AppendFlag(pointer, node.Flags, TypeFlags.Many, "many");
                return pointer.ToString();
            case TypeKind.Slice:
                string slice = "slice<" + TypeText(module, new TypeId(node.A)) + ">";
                return node.Flags.HasFlag(TypeFlags.Const) ? slice + " const" : slice;
            case TypeKind.Optional:
                return "optional<" + TypeText(module, new TypeId(node.A)) + ">";
            case TypeKind.Result:
                return "result<" + TypeText(module, new TypeId(node.A)) + ", " + TypeText(module, new TypeId(node.B)) + ">";
            case TypeKind.ErrorSet:
                return "errorset{" + string.Join(", ", module.TypeLists[node.A].Select(i => module.Strings[i])) + "}";
            case TypeKind.AnyError:
                return "anyerror";
            case TypeKind.Array:
                return "array<" + TypeText(module, new TypeId(node.A)) + ", " + node.B + ">";
            case TypeKind.Fn:
                return "fn" + SigText(module, new SigId(node.A));
            case TypeKind.Struct:
                return "struct " + Reference(module.Text(module.Structs[node.A].Key));
            case TypeKind.Enum:
                return "enum " + Reference(module.Text(module.Enums[node.A].Key));
            case TypeKind.Opaque:
                return "opaque " + Reference(module.Text(module.Opaques[node.A].Key));
            default:
                return IrTypes.Name(node.Kind);
        }
    }

    public static string SigText(Module module, SigId sig)
    {
        SigNode node = module.Sigs[sig.Index];
        var text = new StringBuilder("(");
        int[] items = module.TypeLists[node.Params.Index];
        for (int i = 0; i < items.Length; i++)
        {
            var parameter = SigParam.Decode(items[i]);
            if (i > 0)
            {
                text.Append(", ");
            }

            text.Append(IrNames.ParamMode(parameter.Mode)).Append(TypeText(module, parameter.Type));
        }

        if (node.Variadic)
        {
            text.Append(items.Length > 0 ? ", ..." : "...");
        }

        text.Append(") -> ").Append(IrNames.ParamMode(node.ReturnMode)).Append(TypeText(module, node.Return));
        if (node.Conv != CallConv.Default)
        {
            text.Append(" cc ").Append(IrNames.Conv(node.Conv));
        }

        return text.ToString();
    }

    public static string Quote(string value) => QuoteBytes(Encoding.UTF8.GetBytes(value));

    public static string QuoteBytes(byte[] data)
    {
        var text = new StringBuilder("\"");
        foreach (byte b in data)
        {
            switch (b)
            {
                case (byte)'"':
                    text.Append("\\\"");
                    break;
                case (byte)'\\':
                    text.Append("\\\\");
                    break;
                case (byte)'\n':
                    text.Append("\\n");
                    break;
                case (byte)'\t':
                    text.Append("\\t");
                    break;
                case >= 0x20 and < 0x7F:
                    text.Append((char)b);
                    break;
                default:
                    text.Append("\\x").Append(b.ToString("X2"));
                    break;
            }
        }

        return text.Append('"').ToString();
    }

    public static string Reference(string key) =>
        BareReference().IsMatch(key) && !_keywords.Contains(key) ? key : Quote(key);

    public static List<FunctionId> OrderedFunctions(Module module)
    {
        var ids = new List<FunctionId>();
        for (int i = 0; i < module.Functions.Count; i++)
        {
            ids.Add(new FunctionId(i));
        }

        ids.Sort((a, b) => string.CompareOrdinal(
            module.Text(module.Functions[a.Index].Key),
            module.Text(module.Functions[b.Index].Key)));
        return ids;
    }

    public static List<EnumId> OrderedEnums(Module module)
    {
        var ids = new List<EnumId>();
        for (int i = 0; i < module.Enums.Count; i++)
        {
            ids.Add(new EnumId(i));
        }

        ids.Sort((a, b) => string.CompareOrdinal(
            module.Text(module.Enums[a.Index].Key),
            module.Text(module.Enums[b.Index].Key)));
        return ids;
    }

    private static void PrintEnum(Module module, EnumId id, StringBuilder text)
    {
        EnumDecl declaration = module.Enums[id.Index];
        text.Append("enum ").Append(Reference(module.Text(declaration.Key))).Append(": ").Append(TypeText(module, declaration.Underlying));
        if (declaration.Exhaustive)
        {
            text.Append(" exhaustive");
        }

        PrintOrigin(module, declaration.Origin, text);
        text.Append(' ').Append(Span(declaration.Span)).Append(" {\n");
        for (int i = 0; i < declaration.Members.Count; i++)
        {
            EnumMember member = module.EnumMembers[declaration.Members.Start + i];
            text.Append(Indent).Append(module.Text(member.Name)).Append(" = ").Append(member.Value.ToString()).Append('\n');
        }

        text.Append("}\n");
    }

    public static List<OpaqueId> OrderedOpaques(Module module)
    {
        var ids = new List<OpaqueId>();
        for (int i = 0; i < module.Opaques.Count; i++)
        {
            ids.Add(new OpaqueId(i));
        }

        ids.Sort((a, b) => string.CompareOrdinal(
            module.Text(module.Opaques[a.Index].Key),
            module.Text(module.Opaques[b.Index].Key)));
        return ids;
    }

    private static void PrintCImport(CImportDecl import, int number, StringBuilder text)
    {
        text.Append('\n').Append("cimport ").Append(number).Append(" {\n");
        foreach (string header in import.Headers)
        {
            text.Append(Indent).Append("header ").Append(Quote(header)).Append('\n');
        }

        foreach (string define in import.Defines)
        {
            text.Append(Indent).Append("define ").Append(Quote(define)).Append('\n');
        }

        foreach (string include in import.IncludeDirs)
        {
            text.Append(Indent).Append("include ").Append(Quote(include)).Append('\n');
        }

        text.Append("}\n");
    }

    public static List<GlobalId> OrderedGlobals(Module module)
    {
        var ids = new List<GlobalId>();
        for (int i = 0; i < module.Globals.Count; i++)
        {
            ids.Add(new GlobalId(i));
        }

        ids.Sort((a, b) => string.CompareOrdinal(
            module.Text(module.Globals[a.Index].Key),
            module.Text(module.Globals[b.Index].Key)));
        return ids;
    }

    private static void PrintGlobal(Module module, GlobalId id, StringBuilder text)
    {
        GlobalDecl global = module.Globals[id.Index];
        for (int i = 0; i < global.Attributes.Count; i++)
        {
            AttributeNode attribute = module.Attributes[global.Attributes.Start + i];
            text.Append("#[").Append(Reference(module.Text(attribute.Name))).Append("]\n");
        }

        text.Append("global ").Append(Reference(module.Text(global.Key))).Append(": ").Append(TypeText(module, global.Type));
        if (global.Mutable)
        {
            text.Append(" mutable");
        }

        if (global.ThreadLocal)
        {
            text.Append(" threadlocal");
        }

        switch (global.Linkage)
        {
            case LinkageKind.Export:
                text.Append(" export(").Append(Quote(module.Text(global.ExportName))).Append(')');
                break;
            case LinkageKind.Extern:
                text.Append(" extern");
                break;
        }

        if (!global.Section.IsNone)
        {
            text.Append(" section ").Append(Quote(module.Text(global.Section)));
        }

        if (global.Alignment > 0)
        {
            text.Append(" align ").Append(global.Alignment);
        }

        if (!global.Initializer.IsNone)
        {
            text.Append(" = ");
            PrintConstant(module, global.Initializer, text);
        }

        PrintOrigin(module, global.Origin, text);
        text.Append(' ').Append(Span(global.Span)).Append('\n');
    }

    public static List<StructId> OrderedStructs(Module module)
    {
        var ids = new List<StructId>();
        for (int i = 0; i < module.Structs.Count; i++)
        {
            ids.Add(new StructId(i));
        }

        ids.Sort((a, b) => string.CompareOrdinal(
            module.Text(module.Structs[a.Index].Key),
            module.Text(module.Structs[b.Index].Key)));
        return ids;
    }

    private static void PrintStruct(Module module, StructId id, StringBuilder text)
    {
        StructDecl declaration = module.Structs[id.Index];
        for (int i = 0; i < declaration.Attributes.Count; i++)
        {
            AttributeNode attribute = module.Attributes[declaration.Attributes.Start + i];
            text.Append("#[").Append(Reference(module.Text(attribute.Name))).Append("]\n");
        }

        text.Append("struct ").Append(Reference(module.Text(declaration.Key))).Append(" layout ").Append(declaration.Layout switch
        {
            StructLayoutKind.C => "c",
            StructLayoutKind.Packed => "c.packed(" + declaration.Pack + ")",
            StructLayoutKind.Auto => "auto",
            _ => "explicit",
        });
        if (declaration.Size != StructDecl.NotComputed)
        {
            text.Append(" size ").Append(declaration.Size).Append(" align ").Append(declaration.Alignment);
        }

        if (declaration.MinSize > 0)
        {
            text.Append(" minsize ").Append(declaration.MinSize);
        }

        PrintOrigin(module, declaration.Origin, text);
        text.Append(' ').Append(Span(declaration.Span)).Append(" {\n");
        for (int i = 0; i < declaration.Fields.Count; i++)
        {
            FieldDecl field = module.Fields[declaration.Fields.Start + i];
            text.Append(Indent).Append(module.Text(field.Name)).Append(": ").Append(TypeText(module, field.Type));
            if (field.BitWidth != FieldDecl.NoBits)
            {
                text.Append(" bits ").Append(field.BitWidth);
                if (field.BitOffset != FieldDecl.NoBitOffset)
                {
                    text.Append(" at ").Append(field.BitOffset);
                }
            }
            else if (field.Offset != FieldDecl.NoOffset)
            {
                text.Append(" @").Append(field.Offset);
            }

            text.Append('\n');
        }

        text.Append("}\n");
    }

    private static void AppendFlag(StringBuilder text, TypeFlags flags, TypeFlags flag, string word)
    {
        if (flags.HasFlag(flag))
        {
            text.Append(' ').Append(word);
        }
    }

    private static string Span(SourceSpan span) => "@" + (span.File.Index + 1) + ":" + span.Line + ":" + span.Column;

    private static void PrintFunction(Module module, FunctionId id, StringBuilder text)
    {
        FunctionDecl function = module.Functions[id.Index];
        for (int i = 0; i < function.Attributes.Count; i++)
        {
            AttributeNode attribute = module.Attributes[function.Attributes.Start + i];
            text.Append("#[").Append(Reference(module.Text(attribute.Name))).Append("]\n");
        }

        text.Append("fn ").Append(Reference(module.Text(function.Key))).Append('(');
        for (int i = 0; i < function.Params.Count; i++)
        {
            ParamDecl parameter = module.Params[function.Params.Start + i];
            if (i > 0)
            {
                text.Append(", ");
            }

            text.Append(module.Text(parameter.Name)).Append(": ").Append(IrNames.ParamMode(parameter.Mode))
                .Append(TypeText(module, parameter.Type));
        }

        if (function.Variadic)
        {
            text.Append(function.Params.Count > 0 ? ", ..." : "...");
        }

        text.Append(") -> ").Append(IrNames.ParamMode(function.ReturnMode)).Append(TypeText(module, function.Return));
        if (function.Conv != CallConv.Default)
        {
            text.Append(" cc ").Append(IrNames.Conv(function.Conv));
        }

        switch (function.Linkage)
        {
            case LinkageKind.Export:
                text.Append(" export(").Append(Quote(module.Text(function.ExportName))).Append(')');
                if (function.ExportWeak)
                {
                    text.Append(" weak");
                }

                break;
            case LinkageKind.Extern:
                text.Append(" extern");
                break;
            case LinkageKind.CMacro:
                text.Append(" c.macro");
                break;
        }

        if (function.Inline != InlineKind.Default)
        {
            text.Append(" inline ").Append(function.Inline == InlineKind.Always ? "always" : "never");
        }

        if (function.Safety != SafetyOverride.Default)
        {
            text.Append(" safety ").Append(function.Safety == SafetyOverride.On ? "on" : "off");
        }

        PrintOrigin(module, function.Origin, text);
        text.Append(' ').Append(Span(function.Span));
        Body? body = module.Bodies[id.Index];
        if (body is null)
        {
            text.Append('\n');
            return;
        }

        text.Append(" {\n");
        PrintBody(module, body, text);
        text.Append("}\n");
    }

    private static void PrintOrigin(Module module, Origin origin, StringBuilder text)
    {
        switch (origin.Kind)
        {
            case OriginKind.Source when !origin.Language.IsNone:
                text.Append(" origin source(").Append(module.Text(origin.Language)).Append(')');
                break;
            case OriginKind.CHeader:
                text.Append(" origin cheader(").Append(origin.ImportId).Append(", ")
                    .Append(Quote(module.Text(origin.CName))).Append(')');
                break;
            case OriginKind.CLibrary:
                text.Append(" origin clibrary(").Append(Quote(module.Text(origin.Library))).Append(", ")
                    .Append(Quote(module.Text(origin.CName))).Append(')');
                break;
            case OriginKind.Synthetic:
                text.Append(" origin synthetic");
                break;
        }
    }

    private static void PrintBody(Module module, Body body, StringBuilder text)
    {
        for (int i = 0; i < body.Locals.Count; i++)
        {
            LocalDecl local = body.Locals[i];
            text.Append(Indent).Append("local %").Append(i);
            if (!local.Name.IsNone)
            {
                text.Append(' ').Append(module.Text(local.Name));
            }

            text.Append(": ").Append(TypeText(module, local.Type)).Append(' ').Append(local.Kind switch
            {
                LocalKind.Param => "param",
                LocalKind.User => "user",
                _ => "temp",
            });
            if (!local.Span.IsNone)
            {
                text.Append(' ').Append(Span(local.Span));
            }

            text.Append('\n');
        }

        for (int b = 0; b < body.Blocks.Count; b++)
        {
            text.Append("bb").Append(b).Append(":\n");
            Block block = body.Blocks[b];
            foreach (StatementId statementId in block.Statements)
            {
                text.Append(Indent);
                PrintStatement(module, body, body.Statements[statementId.Index], text);
                text.Append('\n');
            }

            if (block.Terminator is Terminator terminator)
            {
                text.Append(Indent);
                PrintTerminator(module, body, terminator, text);
                text.Append('\n');
            }
        }
    }

    private static void PrintStatement(Module module, Body body, Statement statement, StringBuilder text)
    {
        switch (statement.Kind)
        {
            case StatementKind.Assign:
                PrintPlace(module, body, statement.Place, text);
                text.Append(" = ");
                PrintRvalue(module, body, statement.Rvalue, text);
                break;
            case StatementKind.Call:
                PrintRvalue(module, body, statement.Rvalue, text);
                break;
            case StatementKind.Intrinsic:
                PrintRvalue(module, body, statement.Rvalue, text);
                break;
            case StatementKind.Check:
                text.Append("check ").Append(IrNames.Check((CheckKind)statement.Aux)).Append(' ');
                PrintOperand(module, body, statement.Operand, text);
                text.Append(' ').Append(Quote(module.Text(statement.Message)));
                break;
            case StatementKind.Live:
                text.Append("live %").Append(statement.Aux);
                break;
            case StatementKind.TracePush:
                text.Append("trace.push");
                break;
            case StatementKind.TraceSave:
                text.Append("trace.save %").Append(statement.Aux);
                break;
            case StatementKind.TraceRestore:
                text.Append("trace.restore %").Append(statement.Aux);
                break;
            default:
                text.Append("dead %").Append(statement.Aux);
                break;
        }

        text.Append("  ").Append(Span(statement.Span));
    }

    private static void PrintTerminator(Module module, Body body, Terminator terminator, StringBuilder text)
    {
        switch (terminator.Kind)
        {
            case TermKind.Goto:
                text.Append("goto bb").Append(terminator.Then.Index);
                break;
            case TermKind.Branch:
                text.Append("branch ");
                PrintOperand(module, body, terminator.Operand, text);
                text.Append(" ? bb").Append(terminator.Then.Index).Append(" : bb").Append(terminator.Else.Index);
                break;
            case TermKind.Switch:
                text.Append("switch ");
                PrintOperand(module, body, terminator.Operand, text);
                text.Append(" [");
                for (int i = 0; i < terminator.Cases.Count; i++)
                {
                    SwitchCase item = body.SwitchCases[terminator.Cases.Start + i];
                    if (i > 0)
                    {
                        text.Append(' ');
                    }

                    PrintConstant(module, item.Value, text);
                    text.Append(" -> bb").Append(item.Target.Index);
                }

                text.Append("] default bb").Append(terminator.Else.Index);
                break;
            case TermKind.Return:
                text.Append("return");
                if (!terminator.Operand.IsNone)
                {
                    text.Append(' ');
                    PrintOperand(module, body, terminator.Operand, text);
                }

                break;
            case TermKind.Unreachable:
                text.Append("unreachable ").Append(terminator.Mode switch
                {
                    UnreachableMode.Trap => "trap",
                    UnreachableMode.Assume => "assume",
                    _ => "default",
                });
                break;
            default:
                text.Append("panic ");
                PrintOperand(module, body, terminator.Operand, text);
                break;
        }

        text.Append("  ").Append(Span(terminator.Span));
    }

    private static void PrintPlace(Module module, Body body, PlaceId id, StringBuilder text)
    {
        Place place = body.Places[id.Index];
        string baseText = place.Base switch
        {
            PlaceBase.Local => "%" + place.Id,
            PlaceBase.Global => "global " + Reference(module.Text(module.Globals[place.Id].Key)),
            _ => "(*%" + place.Id + ")",
        };
        text.Append(baseText);
        for (int i = 0; i < place.Projections.Count; i++)
        {
            Projection projection = body.Projections[place.Projections.Start + i];
            switch (projection.Kind)
            {
                case ProjectionKind.Field:
                    text.Append(".field(").Append(projection.Value).Append(')');
                    break;
                case ProjectionKind.Index:
                    text.Append('[');
                    PrintOperand(module, body, new OperandId(projection.Value), text);
                    text.Append(']');
                    break;
                default:
                    text.Append(".payload(").Append(projection.Value).Append(')');
                    break;
            }
        }
    }

    private static void PrintOperand(Module module, Body body, OperandId id, StringBuilder text)
    {
        Operand operand = body.Operands[id.Index];
        switch (operand.Kind)
        {
            case OperandKind.Copy:
                text.Append("copy ");
                PrintPlace(module, body, new PlaceId(operand.A), text);
                break;
            case OperandKind.Const:
                text.Append("const ");
                PrintConstant(module, new ConstId(operand.A), text);
                break;
            case OperandKind.FnRef:
                text.Append("fnref ").Append(Reference(module.Text(module.Functions[operand.A].Key)));
                break;
            default:
                text.Append("undef ").Append(TypeText(module, new TypeId(operand.A)));
                break;
        }
    }

    private static void PrintConstant(Module module, ConstId id, StringBuilder text)
    {
        ConstNode constant = module.Constants[id.Index];
        text.Append(TypeText(module, constant.Type)).Append(' ');
        PrintValue(module, constant, text);
    }

    private static void PrintValue(Module module, ConstNode constant, StringBuilder text)
    {
        switch (constant.Kind)
        {
            case ConstKind.Int:
                text.Append(constant.Value.ToString());
                break;
            case ConstKind.Float:
                text.Append("bits ").Append(((ulong)constant.Value).ToString("x"));
                break;
            case ConstKind.Bool:
                text.Append(constant.Value != 0 ? "true" : "false");
                break;
            case ConstKind.Null:
                text.Append("null");
                break;
            case ConstKind.Error:
                text.Append("error ").Append(module.Strings[(int)constant.Value]);
                break;
            case ConstKind.Bytes:
                text.Append("bytes ").Append(QuoteBytes(module.Bytes[constant.Bytes.Index]));
                break;
            case ConstKind.Aggregate:
                text.Append('{');
                int[] items = module.TypeLists[constant.Items.Index];
                for (int i = 0; i < items.Length; i++)
                {
                    if (i > 0)
                    {
                        text.Append(", ");
                    }

                    PrintValue(module, module.Constants[items[i]], text);
                }

                text.Append('}');
                break;
            default:
                text.Append("undef");
                break;
        }
    }

    private static void PrintRvalue(Module module, Body body, RvalueId id, StringBuilder text)
    {
        Rvalue rvalue = body.Rvalues[id.Index];
        switch (rvalue.Kind)
        {
            case RvalueKind.Use:
                PrintOperand(module, body, rvalue.A, text);
                break;
            case RvalueKind.Binop:
                var binop = (BinOp)rvalue.Op;
                text.Append("binop ").Append(IrNames.BinOp(binop)).Append(' ');
                if (IrNames.HasMode(binop))
                {
                    text.Append(IrNames.Mode(rvalue.Mode)).Append(' ');
                }

                PrintOperand(module, body, rvalue.A, text);
                text.Append(", ");
                PrintOperand(module, body, rvalue.B, text);
                break;
            case RvalueKind.Unop:
                var unop = (UnOp)rvalue.Op;
                text.Append("unop ").Append(IrNames.UnOp(unop)).Append(' ');
                if (unop == UnOp.Neg)
                {
                    text.Append(IrNames.Mode(rvalue.Mode)).Append(' ');
                }

                PrintOperand(module, body, rvalue.A, text);
                break;
            case RvalueKind.Cast:
                var cast = (CastKind)rvalue.Op;
                text.Append("cast ").Append(IrNames.Cast(cast)).Append(' ');
                if (IrNames.HasMode(cast))
                {
                    text.Append(IrNames.Mode(rvalue.Mode)).Append(' ');
                }

                PrintOperand(module, body, rvalue.A, text);
                text.Append(" to ").Append(TypeText(module, rvalue.Type));
                break;
            case RvalueKind.Addr:
                text.Append(rvalue.Aux != 0 ? "addr.mut " : "addr ");
                PrintPlace(module, body, rvalue.Place, text);
                break;
            case RvalueKind.Aggregate:
                text.Append("aggregate ").Append(TypeText(module, rvalue.Type)).Append('(');
                for (int i = 0; i < rvalue.Args.Count; i++)
                {
                    if (i > 0)
                    {
                        text.Append(", ");
                    }

                    PrintOperand(module, body, body.Args[rvalue.Args.Start + i], text);
                }

                text.Append(')');
                break;
            case RvalueKind.Len:
                text.Append("len ");
                PrintPlace(module, body, rvalue.Place, text);
                break;
            case RvalueKind.Slice:
                text.Append("slice ");
                PrintPlace(module, body, rvalue.Place, text);
                text.Append(", ");
                PrintOperand(module, body, rvalue.A, text);
                text.Append(", ");
                PrintOperand(module, body, rvalue.B, text);
                break;
            case RvalueKind.Alloca:
                text.Append("alloca ").Append(TypeText(module, rvalue.Type)).Append(", ");
                PrintOperand(module, body, rvalue.A, text);
                text.Append(", ").Append(IrNames.AllocaInit((AllocaInit)rvalue.Aux));
                break;
            case RvalueKind.HasPayload:
                text.Append("haspayload ");
                PrintPlace(module, body, rvalue.Place, text);
                break;
            case RvalueKind.OptionalSome:
                text.Append("optional.some ");
                PrintOperand(module, body, rvalue.A, text);
                break;
            case RvalueKind.OptionalNone:
                text.Append("optional.none ").Append(TypeText(module, rvalue.Type));
                break;
            case RvalueKind.ResultOk:
                text.Append("result.ok ").Append(TypeText(module, rvalue.Type));
                if (!rvalue.A.IsNone)
                {
                    text.Append(' ');
                    PrintOperand(module, body, rvalue.A, text);
                }

                break;
            case RvalueKind.ResultErr:
                text.Append("result.err ").Append(TypeText(module, rvalue.Type)).Append(' ');
                PrintOperand(module, body, rvalue.A, text);
                break;
            case RvalueKind.Err:
                text.Append("err ");
                PrintPlace(module, body, rvalue.Place, text);
                break;
            case RvalueKind.Errcode:
                text.Append("errcode ");
                PrintOperand(module, body, rvalue.A, text);
                break;
            case RvalueKind.Intrinsic:
                text.Append("intrinsic ").Append(module.Strings[rvalue.Aux]).Append('(');
                for (int i = 0; i < rvalue.Args.Count; i++)
                {
                    if (i > 0)
                    {
                        text.Append(", ");
                    }

                    PrintOperand(module, body, body.Args[rvalue.Args.Start + i], text);
                }

                text.Append(')');
                break;
            case RvalueKind.SizeOf:
                text.Append("sizeof ").Append(TypeText(module, rvalue.Type));
                break;
            case RvalueKind.AlignOf:
                text.Append("alignof ").Append(TypeText(module, rvalue.Type));
                break;
            case RvalueKind.OffsetOf:
                text.Append("offsetof ").Append(TypeText(module, rvalue.Type)).Append(' ').Append(rvalue.Aux);
                break;
            default:
                text.Append("call ");
                if (rvalue.Callee == CalleeKind.Direct)
                {
                    text.Append("direct ").Append(Reference(module.Text(module.Functions[rvalue.CalleeId].Key)));
                }
                else
                {
                    text.Append("indirect ");
                    PrintOperand(module, body, new OperandId(rvalue.CalleeId), text);
                }

                text.Append('(');
                for (int i = 0; i < rvalue.Args.Count; i++)
                {
                    if (i > 0)
                    {
                        text.Append(", ");
                    }

                    PrintOperand(module, body, body.Args[rvalue.Args.Start + i], text);
                }

                text.Append(')');
                break;
        }
    }
}
