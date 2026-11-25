namespace CDelta.Backend.C;

internal static class CPrelude
{
    public const string Includes = """
        #include <stdint.h>
        #include <stddef.h>
        #include <stdbool.h>
        #include <stdalign.h>
        #include <stdnoreturn.h>

        """;

    public const string EmulatedInt128 = """
        typedef struct { uint64_t lo; uint64_t hi; } cd_i128;
        typedef struct { uint64_t lo; uint64_t hi; } cd_u128;

        """;

    public const string UnusedMarker = """
        #if defined(__GNUC__) || defined(__clang__)
        #define CD_UNUSED __attribute__((unused))
        #else
        #define CD_UNUSED
        #endif

        """;

    public const string Alloca = """
        #if defined(_MSC_VER)
        #include <malloc.h>
        #define CD_ALLOCA_RAW(n) _alloca(n)
        #else
        #define CD_ALLOCA_RAW(n) __builtin_alloca(n)
        #endif
        #define cd_alloca(n, a) ((void*)(((uintptr_t)CD_ALLOCA_RAW((n) + (a) - 1) + (a) - 1) & ~(uintptr_t)((a) - 1)))

        """;

    public const string LibcProtocol = """
        void cd_libc_write_stderr(const uint8_t* bytes, size_t length);
        noreturn void cd_libc_abort(void);

        """;

    public const string LibcUnit = """
        #include <stdint.h>
        #include <stddef.h>
        #include <stdnoreturn.h>
        #include <stdio.h>
        #include <stdlib.h>

        void cd_libc_write_stderr(const uint8_t* bytes, size_t length)
        {
            fwrite(bytes, 1, length, stderr);
        }

        noreturn void cd_libc_abort(void)
        {
            abort();
        }

        """;

    public const string PanicWithLibc = """
        static void cd_write_text(const char* text)
        {
            size_t length = 0;
            while (text[length] != 0)
            {
                length++;
            }

            cd_libc_write_stderr((const uint8_t*)text, length);
        }

        static void cd_write_int(int32_t value)
        {
            char digits[16];
            int position = 16;
            uint32_t magnitude = value < 0 ? 0u - (uint32_t)value : (uint32_t)value;
            do
            {
                digits[--position] = (char)('0' + magnitude % 10u);
                magnitude /= 10u;
            } while (magnitude != 0);
            if (value < 0)
            {
                digits[--position] = '-';
            }

            cd_libc_write_stderr((const uint8_t*)(digits + position), (size_t)(16 - position));
        }

        static noreturn void cd_default_panic(const uint8_t* msg, int32_t msg_len, const char* file, int32_t line)
        {
            cd_write_text("panic: ");
            cd_libc_write_stderr(msg, (size_t)msg_len);
            cd_write_text("\n  at ");
            cd_write_text(file);
            cd_write_text(":");
            cd_write_int(line);
            cd_write_text("\n");
            cd_libc_abort();
        }

        """;

    public const string PanicWithoutLibc = """
        static noreturn void cd_default_panic(const uint8_t* msg, int32_t msg_len, const char* file, int32_t line)
        {
            (void)msg;
            (void)msg_len;
            (void)file;
            (void)line;
            __builtin_trap();
        }

        """;

    public const string PanicDirect = """
        static noreturn void cd_panic(const uint8_t* msg, int32_t msg_len, const char* file, int32_t line)
        {
            cd_default_panic(msg, msg_len, file, line);
        }

        """;

    public const string PanicPrototype = """
        static noreturn void cd_panic(const uint8_t* msg, int32_t msg_len, const char* file, int32_t line);

        """;
}
