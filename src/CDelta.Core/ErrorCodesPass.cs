using System.Text;
using CDelta.Diagnostics;
using CDelta.Ir;

namespace CDelta.Core;

public static class ErrorCodesPass
{
    private const int MaxErrorNames = 65535;
    public const string NameTableKey = "synthetic:error_names";

    public static void Run(Module module, DiagnosticBag diagnostics)
    {
        var names = new SortedSet<string>(StringComparer.Ordinal);
        foreach (TypeNode node in module.Types)
        {
            if (node.Kind == TypeKind.ErrorSet)
            {
                names.UnionWith(module.TypeLists[node.A].Select(index => module.Strings[index]));
            }
        }

        foreach (ConstNode constant in module.Constants)
        {
            if (constant.Kind == ConstKind.Error)
            {
                names.Add(module.Strings[(int)constant.Value]);
            }
        }

        if (names.Count > MaxErrorNames)
        {
            diagnostics.Add(
                DiagnosticCode.CD3010,
                new Location(module.Name, 1, 1),
                new Note(null, "the program has " + names.Count + " error names"));
            return;
        }

        int code = 1;
        foreach (string name in names)
        {
            module.ErrorNames[name] = code++;
        }

        GlobalId? table = null;
        if (EntryReturnsResult(module))
        {
            table = DeclareTable(module, new ModuleBuilder(module));
        }

        for (int i = 0; i < module.Bodies.Count; i++)
        {
            if (module.Bodies[i] is { } body)
            {
                table = ReplaceNames(module, body, table);
            }
        }
    }

    private static bool EntryReturnsResult(Module module)
    {
        for (int i = 0; i < module.Functions.Count; i++)
        {
            if (RootsPass.HasAttribute(module, module.Functions[i], RootsPass.EntryPointAttribute)
                && module.Types[module.Functions[i].Return.Index].Kind == TypeKind.Result)
            {
                return true;
            }
        }

        return false;
    }

    private static GlobalId? ReplaceNames(Module module, Body body, GlobalId? table)
    {
        var f = new FunctionBuilder(module, body);
        var builder = new ModuleBuilder(module);
        TypeId u16 = builder.Primitive(TypeKind.U16);
        for (int b = 0; b < body.Blocks.Count; b++)
        {
            Block block = body.Blocks[b];
            var rewritten = new List<StatementId>();
            foreach (StatementId id in block.Statements)
            {
                Statement statement = body.Statements[id.Index];
                if (statement.Kind != StatementKind.Assign || !IsNameLookup(module, body, statement))
                {
                    rewritten.Add(id);
                    continue;
                }

                table ??= DeclareTable(module, builder);
                Rvalue lookup = body.Rvalues[statement.Rvalue.Index];
                f.RedirectStatements(rewritten);
                try
                {
                    LocalId code = f.NewLocal(null, u16, LocalKind.Temp, SourceSpan.None);
                    f.Assign(f.LocalPlace(code), f.Errcode(body.Args[lookup.Args.Start]), statement.Span);
                    OperandId index = f.Copy(f.LocalPlace(code));
                    PlaceId entry = f.Project(f.GlobalPlace(table.Value), ProjectionKind.Index, index.Index);
                    f.Assign(statement.Place, f.Use(f.Copy(entry)), statement.Span);
                }
                finally
                {
                    f.RedirectStatements(null);
                }
            }

            block.Statements.Clear();
            block.Statements.AddRange(rewritten);
        }

        return table;
    }

    private static bool IsNameLookup(Module module, Body body, Statement statement)
    {
        Rvalue rvalue = body.Rvalues[statement.Rvalue.Index];
        return rvalue.Kind == RvalueKind.Intrinsic && module.Strings[rvalue.Aux] == IrTyper.ErrorNameIntrinsic;
    }

    private static GlobalId DeclareTable(Module module, ModuleBuilder builder)
    {
        TypeId bytes = builder.SliceOf(builder.Primitive(TypeKind.U8), true);
        var items = new List<ConstId> { builder.BytesConstant(bytes, []) };
        foreach ((string name, int _) in module.ErrorNames)
        {
            items.Add(builder.BytesConstant(bytes, new UTF8Encoding(false).GetBytes(name)));
        }

        TypeId type = builder.ArrayOf(bytes, items.Count);
        return builder.DeclareGlobal(
            NameTableKey,
            NameTableKey,
            SourceSpan.None,
            Origin.Synthetic,
            type,
            false,
            false,
            builder.AggregateConstant(type, items),
            LinkageKind.Internal,
            null);
    }
}
