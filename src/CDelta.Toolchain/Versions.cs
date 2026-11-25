namespace CDelta.Toolchain;

internal static class Versions
{
    public static bool TryParse(string text, out int major, out int minor, out int patch)
    {
        major = 0;
        minor = 0;
        patch = 0;
        string[] parts = text.Trim().Split('.');
        if (parts.Length == 0 || !int.TryParse(parts[0], out major))
        {
            return false;
        }

        if (parts.Length > 1)
        {
            int.TryParse(parts[1], out minor);
        }

        if (parts.Length > 2)
        {
            int.TryParse(parts[2], out patch);
        }

        return true;
    }

    public static string? FirstVersion(string text)
    {
        foreach (string token in text.Split([' ', '\n', '\r', '\t', '-', '(', ')'], StringSplitOptions.RemoveEmptyEntries))
        {
            if (token.Length > 0 && char.IsDigit(token[0]) && token.Contains('.'))
            {
                return token;
            }
        }

        return null;
    }
}
