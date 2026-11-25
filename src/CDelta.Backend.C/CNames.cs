using System.Text;
using CDelta.Ir;

namespace CDelta.Backend.C;

internal sealed class CNames
{
    private const string FunctionPrefix = "cd_";

    private readonly string[] _functionNames;
    private readonly HashSet<string> _taken = [];

    public CNames(Module module)
    {
        _functionNames = new string[module.Functions.Count];
        var byName = new Dictionary<string, int>();
        for (int i = 0; i < module.Functions.Count; i++)
        {
            string name = module.Text(module.Functions[i].Name);
            byName[name] = byName.TryGetValue(name, out int count) ? count + 1 : 1;
        }

        for (int i = 0; i < module.Functions.Count; i++)
        {
            FunctionDecl function = module.Functions[i];
            string name = NameOf(module, function, byName[module.Text(function.Name)] > 1);
            _functionNames[i] = name;
            _taken.Add(name);
        }
    }

    public string Function(FunctionId id) => _functionNames[id.Index];

    public bool IsTaken(string name) => _taken.Contains(name) || CKeywords.IsReserved(name);

    private static string NameOf(Module module, FunctionDecl function, bool shared)
    {
        switch (function.Linkage)
        {
            case LinkageKind.Export:
                return module.Text(function.ExportName);
            case LinkageKind.Extern:
            case LinkageKind.CMacro:
                return module.Text(function.Origin.CName);
        }

        var text = new StringBuilder(FunctionPrefix);
        string[] segments = module.Text(function.Name).Split('.');
        for (int s = 0; s < segments.Length; s++)
        {
            if (s > 0)
            {
                text.Append('_');
            }

            foreach (char c in segments[s])
            {
                if (c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9')
                {
                    text.Append(c);
                }
                else
                {
                    text.Append("_x").Append(((int)c).ToString("X2"));
                }
            }
        }

        if (shared)
        {
            text.Append("__").Append(CHash.Hash8(module.Text(function.Key)));
        }

        return text.ToString();
    }
}
