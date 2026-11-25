using System.Text;
using ClangSharp.Interop;
using CDelta.Diagnostics;
using CDelta.Ir;
using IrTypeKind = CDelta.Ir.TypeKind;

namespace CDelta.Frontend.C;

public static unsafe class CHeaderReader
{
    private const string SyntheticName = "cdelta_imports.h";
    private const string MacroPrefix = "__cdelta_macro_";
    private const string UnsupportedAttribute = "cdelta.Unsupported";

    public static CImportResult Read(string moduleName, IReadOnlyList<ImportRequest> imports, ReadOptions options, DiagnosticBag diagnostics)
    {
        var module = new Module(moduleName) { Stage = ModuleStage.Lowered };
        var builder = new ModuleBuilder(module);
        var unsupported = new Dictionary<string, string>();
        if (!CheckDefines(imports, diagnostics))
        {
            return new CImportResult(module, [], unsupported);
        }

        string directory = Path.Combine(Path.GetTempPath(), "cdelta-c-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string source = Path.Combine(directory, SyntheticName);
            File.WriteAllText(source, SyntheticSource(imports, options));
            string[] arguments = Arguments(imports, options);
            using var index = CXIndex.Create();
            using CXTranslationUnit unit = Parse(index, source, arguments, diagnostics);
            if (unit.Handle == IntPtr.Zero || diagnostics.HasError)
            {
                return new CImportResult(module, [], unsupported);
            }

            List<CXCursor> cursors = TopLevel(unit);
            IReadOnlyDictionary<string, string> typedefs = TypedefNames(cursors);
            var translator = new CTypeTranslator(builder, 0, typedefs);
            for (int i = 0; i < imports.Count; i++)
            {
                builder.AddCImport(new CImportDecl(imports[i].Headers, imports[i].Defines, imports[i].IncludeDirs));
            }

            foreach (CXCursor cursor in cursors)
            {
                ReadDeclaration(cursor, builder, translator, unsupported);
            }

            while (translator.PendingRecords.Count > 0)
            {
                CXCursor[] pending = [.. translator.PendingRecords];
                translator.ClearPending();
                foreach (CXCursor record in pending)
                {
                    translator.DefineRecord(record);
                }
            }

            IReadOnlyList<ImportedMacro> macros = ReadMacros(index, source, arguments, cursors);
            return new CImportResult(module, macros, unsupported);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    private static bool CheckDefines(IReadOnlyList<ImportRequest> imports, DiagnosticBag diagnostics)
    {
        var seen = new Dictionary<string, string>();
        foreach (ImportRequest import in imports)
        {
            foreach (string define in import.Defines)
            {
                int equals = define.IndexOf('=');
                string name = equals < 0 ? define : define[..equals];
                string value = equals < 0 ? "1" : define[(equals + 1)..];
                if (seen.TryGetValue(name, out string? previous) && previous != value)
                {
                    diagnostics.Add(DiagnosticCode.CD6009, new Location(SyntheticName, 1, 1), new Note(null, "the macro " + name + " is defined as " + previous + " and as " + value));
                    return false;
                }

                seen[name] = value;
            }
        }

        return true;
    }

    private static string SyntheticSource(IReadOnlyList<ImportRequest> imports, ReadOptions options)
    {
        var text = new StringBuilder();
        foreach (string define in options.Defines.Concat(imports.SelectMany(i => i.Defines)).Distinct())
        {
            int equals = define.IndexOf('=');
            text.Append("#define ").Append(equals < 0 ? define + " 1" : define[..equals] + " " + define[(equals + 1)..]).Append('\n');
        }

        foreach (string header in imports.SelectMany(i => i.Headers).Distinct())
        {
            text.Append("#include \"").Append(header).Append("\"\n");
        }

        return text.ToString();
    }

    private static string[] Arguments(IReadOnlyList<ImportRequest> imports, ReadOptions options)
    {
        var arguments = new List<string> { "-target", options.Target, "-std=c11", "-fparse-all-comments" };
        foreach (string directory in options.SystemIncludeDirs)
        {
            arguments.Add("-isystem");
            arguments.Add(directory);
        }

        foreach (string directory in options.IncludeDirs.Concat(imports.SelectMany(i => i.IncludeDirs)).Distinct())
        {
            arguments.Add("-I");
            arguments.Add(directory);
        }

        return [.. arguments];
    }

    private static CXTranslationUnit Parse(CXIndex index, string source, string[] arguments, DiagnosticBag diagnostics)
    {
        CXTranslationUnit_Flags flags = CXTranslationUnit_Flags.CXTranslationUnit_DetailedPreprocessingRecord;
        CXErrorCode code = CXTranslationUnit.TryParse(index, source, arguments, [], flags, out CXTranslationUnit unit);
        if (code != CXErrorCode.CXError_Success)
        {
            diagnostics.Add(DiagnosticCode.CD6001, new Location(source, 1, 1), new Note(null, "libclang could not parse the headers: " + code));
            return default;
        }

        for (uint i = 0; i < unit.NumDiagnostics; i++)
        {
            using CXDiagnostic diagnostic = unit.GetDiagnostic(i);
            if (diagnostic.Severity >= CXDiagnosticSeverity.CXDiagnostic_Error)
            {
                diagnostic.Location.GetFileLocation(out CXFile file, out uint line, out uint column, out _);
                diagnostics.Add(DiagnosticCode.CD6001, new Location(file.Name.ToString(), (int)line, (int)column), new Note(null, diagnostic.Spelling.ToString()));
            }
        }

        return unit;
    }

    private static List<CXCursor> TopLevel(CXTranslationUnit unit)
    {
        var cursors = new List<CXCursor>();
        CXCursorVisitor visitor = (cursor, _, _) =>
        {
            if (!IsFromSynthetic(cursor))
            {
                cursors.Add(cursor);
            }

            return CXChildVisitResult.CXChildVisit_Continue;
        };
        unit.Cursor.VisitChildren(visitor, default);
        return cursors;
    }

    private static bool IsFromSynthetic(CXCursor cursor)
    {
        cursor.Location.GetFileLocation(out CXFile file, out _, out _, out _);
        return file.Handle == IntPtr.Zero || Path.GetFileName(file.Name.ToString()) == SyntheticName;
    }

    private static IReadOnlyDictionary<string, string> TypedefNames(List<CXCursor> cursors)
    {
        var names = new Dictionary<string, string>();
        foreach (CXCursor cursor in cursors.Where(c => c.kind == CXCursorKind.CXCursor_TypedefDecl))
        {
            CXType underlying = cursor.TypedefDeclUnderlyingType.CanonicalType;
            if (underlying.kind is CXTypeKind.CXType_Record or CXTypeKind.CXType_Enum)
            {
                CXCursor target = underlying.Declaration;
                CXCursor definition = target.Definition.IsNull ? target : target.Definition;
                string usr = definition.Usr.ToString();
                string spelling = cursor.Spelling.ToString();
                if (!names.TryGetValue(usr, out string? current) || current.StartsWith('_') && !spelling.StartsWith('_'))
                {
                    names[usr] = spelling;
                }
            }
        }

        return names;
    }

    private static void ReadDeclaration(CXCursor cursor, ModuleBuilder builder, CTypeTranslator translator, Dictionary<string, string> unsupported)
    {
        switch (cursor.kind)
        {
            case CXCursorKind.CXCursor_FunctionDecl:
                ReadFunction(cursor, builder, translator, unsupported);
                break;
            case CXCursorKind.CXCursor_VarDecl when cursor.StorageClass != CX_StorageClass.CX_SC_Static:
                ReadVariable(cursor, builder, translator, unsupported);
                break;
            case CXCursorKind.CXCursor_StructDecl or CXCursorKind.CXCursor_UnionDecl or CXCursorKind.CXCursor_EnumDecl:
                TryTranslate(cursor.Type, translator);
                break;
        }
    }

    private static void TryTranslate(CXType type, CTypeTranslator translator)
    {
        try
        {
            translator.Translate(type);
        }
        catch (UnsupportedTypeException)
        {
        }
    }

    private static void ReadFunction(CXCursor cursor, ModuleBuilder builder, CTypeTranslator translator, Dictionary<string, string> unsupported)
    {
        string name = cursor.Spelling.ToString();
        if (builder.Module.FunctionsByKey.ContainsKey(name))
        {
            return;
        }

        Module module = builder.Module;
        var attributes = new List<string>();
        var parameters = new List<ParamDecl>();
        TypeId result;
        try
        {
            result = translator.Translate(cursor.ResultType);
            for (uint i = 0; i < (uint)cursor.NumArguments; i++)
            {
                CXCursor parameter = cursor.GetArgument(i);
                string parameterName = parameter.Spelling.ToString();
                parameters.Add(new ParamDecl(module.Intern(parameterName.Length > 0 ? parameterName : "p" + i), translator.Translate(parameter.Type), ParamMode.Value));
            }
        }
        catch (UnsupportedTypeException exception)
        {
            attributes.Add(UnsupportedAttribute);
            unsupported["function:" + name] = exception.Message;
            result = builder.Primitive(IrTypeKind.Void);
            parameters.Clear();
        }

        var origin = new Origin(OriginKind.CHeader, StringId.None, 0, StringId.None, module.Intern(name));
        builder.DeclareFunction(name, name, SourceSpan.None, origin, parameters, cursor.IsVariadic, result, CallConv.Default, LinkageKind.Extern, null, attributes);
    }

    private static void ReadVariable(CXCursor cursor, ModuleBuilder builder, CTypeTranslator translator, Dictionary<string, string> unsupported)
    {
        string name = cursor.Spelling.ToString();
        if (builder.Module.GlobalsByKey.ContainsKey(name))
        {
            return;
        }

        TypeId type;
        try
        {
            type = translator.Translate(cursor.Type);
        }
        catch (UnsupportedTypeException exception)
        {
            unsupported["global:" + name] = exception.Message;
            return;
        }

        var origin = new Origin(OriginKind.CHeader, StringId.None, 0, StringId.None, builder.Module.Intern(name));
        builder.DeclareGlobal(name, name, SourceSpan.None, origin, type, true, false, ConstId.None, LinkageKind.Extern, null);
    }

    private static List<ImportedMacro> ReadMacros(CXIndex index, string source, string[] arguments, List<CXCursor> cursors)
    {
        var names = new List<string>();
        foreach (CXCursor cursor in cursors)
        {
            if (cursor.kind == CXCursorKind.CXCursor_MacroDefinition && !cursor.IsMacroFunctionLike && !cursor.Spelling.ToString().StartsWith('_'))
            {
                names.Add(cursor.Spelling.ToString());
            }
        }

        if (names.Count == 0)
        {
            return [];
        }

        string probe = Path.Combine(Path.GetDirectoryName(source)!, "macros.c");
        var text = new StringBuilder(File.ReadAllText(source));
        foreach (string name in names)
        {
            text.Append("static const __typeof__(").Append(name).Append(") ").Append(MacroPrefix).Append(name).Append(" = (").Append(name).Append(");\n");
        }

        File.WriteAllText(probe, text.ToString());
        CXTranslationUnit_Flags flags = CXTranslationUnit_Flags.CXTranslationUnit_None;
        CXErrorCode code = CXTranslationUnit.TryParse(index, probe, arguments, [], flags, out CXTranslationUnit unit);
        var macros = new List<ImportedMacro>();
        if (code != CXErrorCode.CXError_Success)
        {
            return macros;
        }

        using (unit)
        {
            CXCursorVisitor visitor = (cursor, _, _) =>
            {
                if (cursor.kind == CXCursorKind.CXCursor_VarDecl && cursor.Spelling.ToString().StartsWith(MacroPrefix, StringComparison.Ordinal))
                {
                    ImportedMacro? macro = Evaluate(cursor);
                    if (macro is not null)
                    {
                        macros.Add(macro);
                    }
                }

                return CXChildVisitResult.CXChildVisit_Continue;
            };
            unit.Cursor.VisitChildren(visitor, default);
        }

        return macros;
    }

    private static ImportedMacro? Evaluate(CXCursor cursor)
    {
        string name = cursor.Spelling.ToString()[MacroPrefix.Length..];
        CXEvalResult result = cursor.Evaluate;
        if (result.Handle == IntPtr.Zero)
        {
            return null;
        }

        using (result)
        {
            string cType = cursor.Type.CanonicalType.Spelling.ToString().Replace("const ", string.Empty, StringComparison.Ordinal).Trim();
            switch (result.Kind)
            {
                case CXEvalResultKind.CXEval_Int:
                    return new ImportedMacro(name, MacroKind.Integer, cType, result.IsUnsignedInt ? result.AsUnsigned : result.AsLongLong, 0, string.Empty);
                case CXEvalResultKind.CXEval_Float:
                    return new ImportedMacro(name, MacroKind.Float, cType, 0, result.AsDouble, string.Empty);
                case CXEvalResultKind.CXEval_StrLiteral:
                    return new ImportedMacro(name, MacroKind.Text, "char*", 0, 0, result.AsStr.ToString());
                default:
                    return null;
            }
        }
    }
}
