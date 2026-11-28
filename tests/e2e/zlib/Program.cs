[Import("zlib.h")]
static unsafe partial class Z { }

static unsafe class Program
{
    private const int Size = 4096;
    private const int Slack = 64;
    private const int DefaultLevel = -1;

    static int Main()
    {
        byte* original = stackalloc byte[Size];
        byte* packed = stackalloc byte[Size + Slack];
        byte* restored = stackalloc byte[Size];
        for (int i = 0; i < Size; i++)
        {
            original[i] = (byte)(i % 7);
        }

        Z.z_stream deflating = default;
        if (Z.deflateInit_(&deflating, DefaultLevel, Z.zlibVersion(), sizeof(Z.z_stream)) != Z.Z_OK)
        {
            return 1;
        }

        deflating.next_in = original;
        deflating.avail_in = Size;
        deflating.next_out = packed;
        deflating.avail_out = Size + Slack;
        if (Z.deflate(&deflating, Z.Z_FINISH) != Z.Z_STREAM_END)
        {
            return 2;
        }

        int packedLength = Size + Slack - (int)deflating.avail_out;
        Z.deflateEnd(&deflating);

        Z.z_stream inflating = default;
        if (Z.inflateInit_(&inflating, Z.zlibVersion(), sizeof(Z.z_stream)) != Z.Z_OK)
        {
            return 3;
        }

        inflating.next_in = packed;
        inflating.avail_in = (uint)packedLength;
        inflating.next_out = restored;
        inflating.avail_out = Size;
        if (Z.inflate(&inflating, Z.Z_FINISH) != Z.Z_STREAM_END)
        {
            return 4;
        }

        Z.inflateEnd(&inflating);
        for (int i = 0; i < Size; i++)
        {
            if (restored[i] != original[i])
            {
                return 5;
            }
        }

        return packedLength < Size ? 42 : 6;
    }
}
