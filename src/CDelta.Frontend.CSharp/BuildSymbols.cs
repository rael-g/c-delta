namespace CDelta.Frontend.CSharp;

public static class BuildSymbols
{
    public static IReadOnlyList<string> For(string target, string mode)
    {
        string[] parts = target.Split('-');
        var symbols = new List<string>
        {
            "ARCH_" + parts[0].ToUpperInvariant(),
            "OS_" + parts[1].ToUpperInvariant(),
            "MODE_" + (mode switch
            {
                "ReleaseSafe" => "RELEASE_SAFE",
                "ReleaseFast" => "RELEASE_FAST",
                "ReleaseSmall" => "RELEASE_SMALL",
                _ => "DEBUG",
            }),
        };
        if (parts.Length > 2 && parts[2] != "none")
        {
            symbols.Add("ABI_" + parts[2].ToUpperInvariant());
        }

        if (parts[1] != "freestanding")
        {
            symbols.Add("LINK_LIBC");
        }

        return symbols;
    }
}
