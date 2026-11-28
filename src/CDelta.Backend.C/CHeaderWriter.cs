using System.Text;
using CDelta.Ir;

namespace CDelta.Backend.C;

internal static class CHeaderWriter
{
    private const string GuardSuffix = "_H";

    public static string? Write(Module module, CNames names, string packageName)
    {
        List<FunctionId> exports = [.. IrPrinter.OrderedFunctions(module).Where(id => IsExport(module.Functions[id.Index]))];
        if (exports.Count == 0)
        {
            return null;
        }

        string guard = Guard(packageName);
        var text = new StringBuilder();
        text.Append("#ifndef ").Append(guard).Append("\n#define ").Append(guard).Append("\n\n");
        text.Append("#include <stdint.h>\n#include <stddef.h>\n#include <stdbool.h>\n\n");
        text.Append("#ifdef __cplusplus\nextern \"C\" {\n#endif\n\n");
        var pointers = new SortedSet<int>();
        foreach (FunctionId id in exports)
        {
            FunctionDecl function = module.Functions[id.Index];
            CollectFunctionPointers(module, function.Return, pointers);
            for (int i = 0; i < function.Params.Count; i++)
            {
                CollectFunctionPointers(module, module.Params[function.Params.Start + i].Type, pointers);
            }
        }

        foreach (int pointer in pointers)
        {
            text.Append(CTypes.FunctionPointerDefinition(module, names, new TypeId(pointer)));
        }

        if (pointers.Count > 0)
        {
            text.Append('\n');
        }

        foreach (FunctionId id in exports)
        {
            text.Append(CFunctionWriter.Signature(module, id, names, false, null)).Append(";\n");
        }

        text.Append("\n#ifdef __cplusplus\n}\n#endif\n\n#endif\n");
        return text.ToString();
    }

    private static bool IsExport(FunctionDecl function) =>
        function.Linkage == LinkageKind.Export && function.Origin.Kind != OriginKind.Synthetic;

    private static string Guard(string packageName)
    {
        var text = new StringBuilder();
        foreach (char c in packageName)
        {
            text.Append(char.IsAsciiLetterOrDigit(c) ? char.ToUpperInvariant(c) : '_');
        }

        return text.Append(GuardSuffix).ToString();
    }

    private static void CollectFunctionPointers(Module module, TypeId type, SortedSet<int> found)
    {
        TypeNode node = module.Types[type.Index];
        if (node.Kind == TypeKind.Ptr)
        {
            CollectFunctionPointers(module, new TypeId(node.A), found);
            return;
        }

        if (node.Kind != TypeKind.Fn || !found.Add(type.Index))
        {
            return;
        }

        SigNode sig = module.Sigs[node.A];
        CollectFunctionPointers(module, sig.Return, found);
        foreach (int item in module.TypeLists[sig.Params.Index])
        {
            CollectFunctionPointers(module, SigParam.Decode(item).Type, found);
        }
    }
}
