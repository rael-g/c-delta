using System.Text;

namespace CDelta.Backend.C;

internal static class CHash
{
    private const ulong OffsetBasis = 14695981039346656037UL;
    private const ulong Prime = 1099511628211UL;
    private const int HighShift = 32;

    public static string Hash8(string key)
    {
        ulong hash = OffsetBasis;
        foreach (byte b in Encoding.UTF8.GetBytes(key))
        {
            hash ^= b;
            hash *= Prime;
        }

        uint folded = (uint)(hash >> HighShift) ^ (uint)hash;
        return folded.ToString("x8");
    }
}
