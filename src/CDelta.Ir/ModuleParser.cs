namespace CDelta.Ir;

internal sealed class ModuleParser
{
    private readonly string[] _lines;
    private readonly List<int> _sourceLines = [];
    private Module _module = null!;
    private ModuleBuilder _builder = null!;
    private TypeParser _types = null!;

    private readonly Func<string, Target?>? _targets;

    public ModuleParser(string text, Func<string, Target?>? targets)
    {
        _lines = text.Split('\n');
        _targets = targets;
    }

    public Module Run()
    {
        ParseHeader();
        _types = new TypeParser(_module, _builder);
        var items = new List<(int Start, int End, List<string> Attributes)>();
        var pending = new List<string>();
        int i = 1;
        while (i < _lines.Length)
        {
            string line = _lines[i];
            if (line.Length == 0)
            {
                i++;
                continue;
            }

            if (line.StartsWith("source ", StringComparison.Ordinal))
            {
                ParseSource(line, i);
                i++;
                continue;
            }

            if (line.StartsWith("error ", StringComparison.Ordinal))
            {
                var errorCursor = new IrCursor(line, i + 1);
                errorCursor.ExpectWord("error");
                string errorName = errorCursor.ReadWord();
                errorCursor.Expect('=');
                _module.ErrorNames[errorName] = errorCursor.ReadInt();
                i++;
                continue;
            }

            if (line.StartsWith("#[", StringComparison.Ordinal))
            {
                var cursor = new IrCursor(line, i + 1);
                cursor.ExpectText("#[");
                pending.Add(cursor.ReadReference());
                cursor.Expect(']');
                i++;
                continue;
            }

            int end = i;
            if (line.TrimEnd().EndsWith('{'))
            {
                while (end < _lines.Length && _lines[end] != "}")
                {
                    end++;
                }
            }

            items.Add((i, end, pending));
            pending = [];
            i = end + 1;
        }

        DeclareShells(items);
        foreach ((int start, int end, List<string> attributes) in items)
        {
            string kind = FirstWord(_lines[start]);
            if (kind == "struct")
            {
                DefineStruct(start, end);
            }
        }

        foreach ((int start, int end, List<string> attributes) in items)
        {
            if (FirstWord(_lines[start]) == "global")
            {
                DeclareGlobal(start);
            }
        }

        var bodies = new List<(FunctionId Id, int Start, int End)>();
        foreach ((int start, int end, List<string> attributes) in items)
        {
            if (FirstWord(_lines[start]) == "fn")
            {
                bodies.Add((DeclareFunction(start, attributes), start, end));
            }
        }

        foreach ((FunctionId id, int start, int end) in bodies)
        {
            if (end > start)
            {
                Body body = new BodyParser(_module, _types, _lines, start + 1, end).Run();
                _builder.SetBody(id, body);
            }
        }

        return _module;
    }

    private static string FirstWord(string line)
    {
        int space = line.IndexOf(' ');
        return space < 0 ? line : line[..space];
    }

    private void ParseHeader()
    {
        var cursor = new IrCursor(_lines[0], 1);
        cursor.ExpectWord("module");
        string name = cursor.ReadReference();
        cursor.ExpectWord("stage");
        string stage = cursor.ReadWord();
        _module = new Module(name)
        {
            Stage = stage switch
            {
                "lowered" => ModuleStage.Lowered,
                "instantiated" => ModuleStage.Instantiated,
                "final" => ModuleStage.Final,
                _ => throw cursor.Error("unknown stage"),
            },
        };
        _builder = new ModuleBuilder(_module);
        if (cursor.TryWord("target"))
        {
            string triple = cursor.ReadToken();
            _module.Target = _targets?.Invoke(triple) ?? TargetFor(triple, cursor);
        }
    }

    private static Target TargetFor(string triple, IrCursor cursor) => new(triple, 0, 0, TypeKind.I32, true, true, true, BitFieldRules.SysV);

    private void ParseSource(string line, int index)
    {
        var cursor = new IrCursor(line, index + 1);
        cursor.ExpectWord("source");
        cursor.ReadInt();
        string full = cursor.ReadString();
        string display = cursor.ReadString();
        _builder.AddSource(full, display);
    }

    private void DeclareShells(List<(int Start, int End, List<string> Attributes)> items)
    {
        foreach ((int start, int end, List<string> attributes) in items)
        {
            string kind = FirstWord(_lines[start]);
            if (kind == "enum")
            {
                DeclareEnum(start, end);
            }
            else if (kind == "struct")
            {
                DeclareStructShell(start, attributes);
            }
            else if (kind == "opaque")
            {
                DeclareOpaque(start);
            }
            else if (kind == "cimport")
            {
                DeclareCImport(start, end);
            }
        }
    }

    private void DeclareOpaque(int start)
    {
        var cursor = new IrCursor(_lines[start], start + 1);
        cursor.ExpectWord("opaque");
        string key = cursor.ReadReference();
        Origin origin = ParseOrigin(cursor);
        _builder.DeclareOpaque(key, key, ParseSpan(cursor), origin);
    }

    private void DeclareCImport(int start, int end)
    {
        var headers = new List<string>();
        var defines = new List<string>();
        var includes = new List<string>();
        for (int i = start + 1; i < end; i++)
        {
            var cursor = new IrCursor(_lines[i], i + 1);
            string word = cursor.ReadWord();
            string value = cursor.ReadString();
            (word == "header" ? headers : word == "define" ? defines : includes).Add(value);
        }

        _builder.AddCImport(new CImportDecl(headers, defines, includes));
    }

    private void DeclareEnum(int start, int end)
    {
        var cursor = new IrCursor(_lines[start], start + 1);
        cursor.ExpectWord("enum");
        string key = cursor.ReadReference();
        cursor.Expect(':');
        TypeId underlying = _types.ParseType(cursor);
        bool exhaustive = cursor.TryWord("exhaustive");
        Origin origin = ParseOrigin(cursor);
        SourceSpan span = ParseSpan(cursor);
        var members = new List<(string Name, Int128 Value)>();
        for (int i = start + 1; i < end; i++)
        {
            var member = new IrCursor(_lines[i], i + 1);
            string name = member.ReadWord();
            member.Expect('=');
            members.Add((name, member.ReadInt128()));
        }

        _builder.DeclareEnum(key, key, span, origin, underlying, exhaustive, members);
    }

    private void DeclareStructShell(int start, List<string> attributes)
    {
        var cursor = new IrCursor(_lines[start], start + 1);
        cursor.ExpectWord("struct");
        string key = cursor.ReadReference();
        cursor.ExpectWord("layout");
        string layoutWord = cursor.ReadWord();
        int pack = 0;
        StructLayoutKind layout;
        switch (layoutWord)
        {
            case "c":
                layout = StructLayoutKind.C;
                break;
            case "c.packed":
                layout = StructLayoutKind.Packed;
                cursor.Expect('(');
                pack = cursor.ReadInt();
                cursor.Expect(')');
                break;
            case "auto":
                layout = StructLayoutKind.Auto;
                break;
            default:
                layout = StructLayoutKind.Explicit;
                break;
        }

        int size = StructDecl.NotComputed;
        int align = StructDecl.NotComputed;
        if (cursor.TryWord("size"))
        {
            size = cursor.ReadInt();
            cursor.ExpectWord("align");
            align = cursor.ReadInt();
        }

        int minSize = cursor.TryWord("minsize") ? cursor.ReadInt() : 0;
        Origin origin = ParseOrigin(cursor);
        SourceSpan span = ParseSpan(cursor);
        StructId id = _builder.BeginStruct(key, key, span, origin, layout, pack, minSize, attributes);
        _module.Structs[id.Index] = _module.Structs[id.Index] with { Size = size, Alignment = align };
    }

    private void DefineStruct(int start, int end)
    {
        var header = new IrCursor(_lines[start], start + 1);
        header.ExpectWord("struct");
        StructId id = _module.StructsByKey[header.ReadReference()];
        var fields = new List<FieldDecl>();
        for (int i = start + 1; i < end; i++)
        {
            var cursor = new IrCursor(_lines[i], i + 1);
            string name = cursor.ReadWord();
            cursor.Expect(':');
            TypeId type = _types.ParseType(cursor);
            int bits = FieldDecl.NoBits;
            int bitOffset = FieldDecl.NoBitOffset;
            int offset = FieldDecl.NoOffset;
            if (cursor.TryWord("bits"))
            {
                bits = cursor.ReadInt();
                if (cursor.TryWord("at"))
                {
                    bitOffset = cursor.ReadInt();
                    offset = bitOffset / 8;
                }
            }
            else if (cursor.TryChar('@'))
            {
                offset = cursor.ReadInt();
            }

            fields.Add(new FieldDecl(_module.Intern(name), type, IdRange.Empty, bits, offset, bitOffset));
        }

        _builder.DefineFields(id, fields);
    }

    private void DeclareGlobal(int start)
    {
        var cursor = new IrCursor(_lines[start], start + 1);
        cursor.ExpectWord("global");
        string key = cursor.ReadReference();
        cursor.Expect(':');
        TypeId type = _types.ParseType(cursor);
        bool mutable = false;
        bool threadLocal = false;
        LinkageKind linkage = LinkageKind.Internal;
        string? exportName = null;
        string? section = null;
        int alignment = 0;
        while (true)
        {
            if (cursor.TryWord("mutable"))
            {
                mutable = true;
            }
            else if (cursor.TryWord("threadlocal"))
            {
                threadLocal = true;
            }
            else if (cursor.TryWord("extern"))
            {
                linkage = LinkageKind.Extern;
            }
            else if (cursor.TryWord("export"))
            {
                linkage = LinkageKind.Export;
                cursor.Expect('(');
                exportName = cursor.ReadString();
                cursor.Expect(')');
            }
            else if (cursor.TryWord("section"))
            {
                section = cursor.ReadString();
            }
            else if (cursor.TryWord("align"))
            {
                alignment = cursor.ReadInt();
            }
            else
            {
                break;
            }
        }

        ConstId initializer = ConstId.None;
        if (cursor.TryChar('='))
        {
            initializer = _types.ParseConstant(cursor);
        }

        Origin origin = ParseOrigin(cursor);
        SourceSpan span = ParseSpan(cursor);
        GlobalId id = _builder.DeclareGlobal(key, key, span, origin, type, mutable, threadLocal, initializer, linkage, exportName);
        _module.Globals[id.Index] = _module.Globals[id.Index] with
        {
            Section = section is null ? StringId.None : _module.Intern(section),
            Alignment = alignment,
        };
    }

    private FunctionId DeclareFunction(int start, List<string> attributes)
    {
        var cursor = new IrCursor(_lines[start], start + 1);
        cursor.ExpectWord("fn");
        string key = cursor.ReadReference();
        var typeParams = new List<string>();
        if (cursor.TryChar('<'))
        {
            do
            {
                cursor.ExpectWord("type");
                typeParams.Add(cursor.ReadWord());
            }
            while (cursor.TryChar(','));
            cursor.Expect('>');
        }

        cursor.Expect('(');
        var parameters = new List<ParamDecl>();
        bool variadic = false;
        while (!cursor.TryChar(')'))
        {
            if (parameters.Count > 0 || variadic)
            {
                cursor.Expect(',');
            }

            if (cursor.TryText("..."))
            {
                variadic = true;
                continue;
            }

            string name = cursor.ReadWord();
            cursor.Expect(':');
            ParamMode mode = _types.ParseMode(cursor);
            parameters.Add(new ParamDecl(_module.Intern(name), _types.ParseType(cursor), mode));
        }

        cursor.ExpectText("->");
        ParamMode returnMode = _types.ParseMode(cursor);
        TypeId result = _types.ParseType(cursor);
        CallConv conv = CallConv.Default;
        LinkageKind linkage = LinkageKind.Internal;
        string? exportName = null;
        bool weak = false;
        InlineKind inline = InlineKind.Default;
        SafetyOverride safety = SafetyOverride.Default;
        while (true)
        {
            if (cursor.TryWord("cc"))
            {
                conv = IrNames.ParseConv(cursor.ReadWord());
            }
            else if (cursor.TryWord("export"))
            {
                linkage = LinkageKind.Export;
                cursor.Expect('(');
                exportName = cursor.ReadString();
                cursor.Expect(')');
                weak = cursor.TryWord("weak");
            }
            else if (cursor.TryWord("extern"))
            {
                linkage = LinkageKind.Extern;
            }
            else if (cursor.TryWord("c.macro"))
            {
                linkage = LinkageKind.CMacro;
            }
            else if (cursor.TryWord("inline"))
            {
                inline = cursor.ReadWord() == "always" ? InlineKind.Always : InlineKind.Never;
            }
            else if (cursor.TryWord("safety"))
            {
                safety = cursor.ReadWord() == "on" ? SafetyOverride.On : SafetyOverride.Off;
            }
            else
            {
                break;
            }
        }

        Origin origin = ParseOrigin(cursor);
        SourceSpan span = ParseSpan(cursor);
        FunctionId id = _builder.DeclareFunction(key, key, span, origin, parameters, variadic, result, conv, linkage, exportName, attributes, returnMode);
        _module.Functions[id.Index] = _module.Functions[id.Index] with { Inline = inline, Safety = safety, ExportWeak = weak };
        _builder.SetTypeParams(id, typeParams);
        return id;
    }

    private Origin ParseOrigin(IrCursor cursor)
    {
        if (!cursor.TryWord("origin"))
        {
            return Origin.FromSource(StringId.None);
        }

        string kind = cursor.ReadWord();
        switch (kind)
        {
            case "synthetic":
                return Origin.Synthetic;
            case "source":
                cursor.Expect('(');
                string language = cursor.ReadWord();
                cursor.Expect(')');
                return Origin.FromSource(_module.Intern(language));
            case "clibrary":
                cursor.Expect('(');
                string library = cursor.ReadString();
                cursor.Expect(',');
                string cName = cursor.ReadString();
                cursor.Expect(')');
                return Origin.FromLibrary(_module.Intern(library), _module.Intern(cName));
            default:
                cursor.Expect('(');
                int import = cursor.ReadInt();
                cursor.Expect(',');
                string header = cursor.ReadString();
                cursor.Expect(')');
                return new Origin(OriginKind.CHeader, StringId.None, import, StringId.None, _module.Intern(header));
        }
    }

    private static SourceSpan ParseSpan(IrCursor cursor) => SpanParser.Parse(cursor);
}
