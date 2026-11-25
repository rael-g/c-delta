namespace CDelta.Toolchain;

internal static class TargetTriples
{
    private static readonly (string Target, string Zig, string Clang)[] _table =
    [
        ("x86_64-linux-gnu", "x86_64-linux-gnu", "x86_64-unknown-linux-gnu"),
        ("x86_64-linux-musl", "x86_64-linux-musl", "x86_64-unknown-linux-musl"),
        ("aarch64-linux-gnu", "aarch64-linux-gnu", "aarch64-unknown-linux-gnu"),
        ("x86_64-windows-gnu", "x86_64-windows-gnu", "x86_64-w64-windows-gnu"),
        ("x86_64-windows-msvc", "x86_64-windows-msvc", "x86_64-pc-windows-msvc"),
        ("aarch64-macos-none", "aarch64-macos-none", "arm64-apple-macos"),
        ("wasm32-wasi-musl", "wasm32-wasi-musl", "wasm32-unknown-wasi"),
        ("x86_64-freestanding-none", "x86_64-freestanding-none", "x86_64-unknown-none-elf"),
    ];

    public static string? Zig(string target)
    {
        foreach ((string Target, string Zig, string Clang) row in _table)
        {
            if (row.Target == target)
            {
                return row.Zig;
            }
        }

        return null;
    }

    public static string? Clang(string target)
    {
        foreach ((string Target, string Zig, string Clang) row in _table)
        {
            if (row.Target == target)
            {
                return row.Clang;
            }
        }

        return null;
    }
}
