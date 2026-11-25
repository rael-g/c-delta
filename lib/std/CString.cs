namespace Std.C;

public unsafe struct CString
{
    public byte* Pointer;

    public ReadOnlySpan<byte> AsSpan()
    {
        return new ReadOnlySpan<byte>(Pointer, Length());
    }

    private int Length()
    {
        int length = 0;
        while (Pointer[length] != 0)
        {
            length++;
        }

        return length;
    }
}
