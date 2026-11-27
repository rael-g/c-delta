using System.Text;

static class P
{
    public static int Main()
    {
        string text = "héllo";
        int runes = 0;
        int accented = 0;
        foreach (Rune r in text.EnumerateRunes())
        {
            runes++;
            if (r.Value == 233)
            {
                accented += 37;
            }
        }

        var enumerator = text.EnumerateRunes();
        int manual = 0;
        while (enumerator.MoveNext())
        {
            manual++;
        }

        return runes + accented + manual;
    }
}
