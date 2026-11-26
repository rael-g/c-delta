struct S
{
    public char Field;
    public const char Constant = 'a';

    static char Return(char parameter)
    {
        char local = 'b';
        return local;
    }

    static bool Compare(byte value)
    {
        return value == 'a';
    }
}
