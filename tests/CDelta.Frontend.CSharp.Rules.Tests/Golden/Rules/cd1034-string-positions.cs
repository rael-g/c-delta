static class Program
{
    static int Measure(string s)
    {
        int a = s.Length;
        byte b = s[0];
        int c = s.IndexOf("x");
        string d = s.Substring(1);
        string e = s.Substring(1, 2);
        return a + b + c + d.Slice(0, 1).Bytes.Length + e.Bytes.Length + s.Count();
    }
}
