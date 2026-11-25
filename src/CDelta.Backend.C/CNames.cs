using System.Text;
using CDelta.Ir;

namespace CDelta.Backend.C;

internal sealed class CNames
{
    private const string FunctionPrefix = "cd_";
    private const string TypePrefix = "cd_T_";

    private readonly string[] _functionNames;
    private readonly string[] _structNames;
    private readonly string[] _globalNames;
    private readonly HashSet<string> _taken = [];

    public CNames(Module module)
    {
        _globalNames = new string[module.Globals.Count];
        var globalsByName = new Dictionary<string, int>();
        for (int i = 0; i < module.Globals.Count; i++)
        {
            string name = module.Text(module.Globals[i].Name);
            globalsByName[name] = globalsByName.TryGetValue(name, out int count) ? count + 1 : 1;
        }

        for (int i = 0; i < module.Globals.Count; i++)
        {
            GlobalDecl global = module.Globals[i];
            string name = module.Text(global.Name);
            string cName = global.Linkage switch
            {
                LinkageKind.Export => module.Text(global.ExportName),
                LinkageKind.Extern => module.Text(global.Origin.CName),
                _ => FunctionPrefix + Mangle(name) + (globalsByName[name] > 1 ? "__" + CHash.Hash8(module.Text(global.Key)) : string.Empty),
            };
            _globalNames[i] = cName;
            _taken.Add(cName);
        }

        _structNames = new string[module.Structs.Count];
        var structsByName = new Dictionary<string, int>();
        for (int i = 0; i < module.Structs.Count; i++)
        {
            string name = module.Text(module.Structs[i].Name);
            structsByName[name] = structsByName.TryGetValue(name, out int count) ? count + 1 : 1;
        }

        for (int i = 0; i < module.Structs.Count; i++)
        {
            StructDecl declaration = module.Structs[i];
            string name = module.Text(declaration.Name);
            string mangled = TypePrefix + Mangle(name);
            _structNames[i] = structsByName[name] > 1 ? mangled + "__" + CHash.Hash8(module.Text(declaration.Key)) : mangled;
            _taken.Add(_structNames[i]);
        }

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

    public string Struct(StructId id) => _structNames[id.Index];

    public string Global(GlobalId id) => _globalNames[id.Index];

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
        text.Append(Mangle(module.Text(function.Name)));

        if (shared)
        {
            text.Append("__").Append(CHash.Hash8(module.Text(function.Key)));
        }

        return text.ToString();
    }

    public static string MangleSpelling(string spelling)
    {
        var text = new StringBuilder();
        foreach (char c in spelling)
        {
            if (c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '_')
            {
                text.Append(c);
            }
            else if (c == '*')
            {
                text.Append("_p");
            }
            else if (c == ' ')
            {
                text.Append('_');
            }
            else
            {
                text.Append("_x").Append(((int)c).ToString("X2"));
            }
        }

        return text.ToString();
    }

    private static string Mangle(string path)
    {
        var text = new StringBuilder();
        string[] segments = path.Split('.');
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

        return text.ToString();
    }
}
