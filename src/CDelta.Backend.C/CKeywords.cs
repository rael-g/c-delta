namespace CDelta.Backend.C;

internal static class CKeywords
{
    private static readonly HashSet<string> _reserved =
    [
        "auto", "break", "case", "char", "const", "continue", "default", "do", "double", "else", "enum", "extern",
        "float", "for", "goto", "if", "inline", "int", "long", "register", "restrict", "return", "short", "signed",
        "sizeof", "static", "struct", "switch", "typedef", "union", "unsigned", "void", "volatile", "while",
        "_Alignas", "_Alignof", "_Atomic", "_Bool", "_Complex", "_Generic", "_Imaginary", "_Noreturn",
        "_Static_assert", "_Thread_local", "bool", "true", "false", "NULL", "main", "noreturn", "alignas",
        "alignof", "int8_t", "int16_t", "int32_t", "int64_t", "uint8_t", "uint16_t", "uint32_t", "uint64_t",
        "intptr_t", "uintptr_t", "size_t", "asm", "typeof",
    ];

    public static bool IsReserved(string name) => _reserved.Contains(name);
}
