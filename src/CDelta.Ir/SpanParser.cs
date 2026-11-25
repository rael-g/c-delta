namespace CDelta.Ir;

internal static class SpanParser
{
    public static SourceSpan Parse(IrCursor cursor)
    {
        cursor.Expect('@');
        int file = cursor.ReadInt();
        cursor.Expect(':');
        int line = cursor.ReadInt();
        cursor.Expect(':');
        int column = cursor.ReadInt();
        return new SourceSpan(new SourceId(file - 1), line, column);
    }
}
