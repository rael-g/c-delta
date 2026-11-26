using System.Text;

struct Player
{
    public string Name;
    public int Score;
}

static class M
{
    static int Count(string text)
    {
        int count = 0;
        foreach (Rune rune in text)
        {
            count++;
        }

        switch (text)
        {
            case "help":
                return 1;
            default:
                return count;
        }
    }

    static bool Same(string a, string b)
    {
        return a == b || a.Bytes.Length == 0 || a.Slice(0, 1) != b;
    }
}
