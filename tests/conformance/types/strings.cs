using System.Text;

struct Player
{
    public string Name;
    public int Score;
}

static class P
{
    static int Classify(string command)
    {
        switch (command)
        {
            case "help": return 1;
            case "quit": return 2;
            default: return 0;
        }
    }

    public static int Main()
    {
        string a = "héllo";
        string b = "héllo";
        int total = 0;
        if (a == b)
        {
            total += 1;
        }

        if (a != "world")
        {
            total += 2;
        }

        total += Classify("quit") * 10;
        total += Classify("help") * 100;
        int runes = 0;
        foreach (Rune r in a)
        {
            runes++;
            if (r.Value == 233)
            {
                total += 1000;
            }
        }

        total += runes * 10000;
        Player p = default;
        p.Name = "bob";
        total += p.Name.Bytes.Length * 100000;
        string tail = a.Slice(3, 3);
        total += tail == "llo" ? 1000000 : 0;
        total += a.Bytes[0] == 104 ? 10000000 : 0;
        return total % 251;
    }
}
