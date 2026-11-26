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

    public const string TextEql = """
        static inline bool cd_text_eql(cd_T_slice_const_uint8_t a, cd_T_slice_const_uint8_t b)
        {
            return a.len == b.len && (a.len == 0 || __builtin_memcmp(a.ptr, b.ptr, (size_t)a.len) == 0);
        }

        """;

    public const string Utf8Next = """
        static inline int32_t cd_utf8_next(cd_T_slice_const_uint8_t s, int32_t* offset)
        {
            int32_t at = *offset;
            uint32_t lead = s.ptr[at];
            int32_t more;
            uint32_t scalar;
            uint32_t minimum;
            if (lead < 0x80u)
            {
                *offset = at + 1;
                return (int32_t)lead;
            }
            else if (lead >= 0xC2u && lead <= 0xDFu)
            {
                more = 1;
                scalar = lead & 0x1Fu;
                minimum = 0x80u;
            }
            else if (lead >= 0xE0u && lead <= 0xEFu)
            {
                more = 2;
                scalar = lead & 0x0Fu;
                minimum = 0x800u;
            }
            else if (lead >= 0xF0u && lead <= 0xF4u)
            {
                more = 3;
                scalar = lead & 0x07u;
                minimum = 0x10000u;
            }
            else
            {
                *offset = at + 1;
                return 0xFFFD;
            }

            if (s.len - at <= more)
            {
                *offset = at + 1;
                return 0xFFFD;
            }

            for (int32_t k = 1; k <= more; k++)
            {
                uint32_t next = s.ptr[at + k];
                if ((next & 0xC0u) != 0x80u)
                {
                    *offset = at + 1;
                    return 0xFFFD;
                }

                scalar = (scalar << 6) | (next & 0x3Fu);
            }

            if (scalar < minimum || scalar > 0x10FFFFu || (scalar >= 0xD800u && scalar <= 0xDFFFu))
            {
                *offset = at + 1;
                return 0xFFFD;
            }

            *offset = at + more + 1;
            return (int32_t)scalar;
        }

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

    public const string TraceCounter = """
        static _Thread_local uint32_t cd_trace_len;

        """;

    public const string TraceRing = """
        #define CD_TRACE_CAPACITY 32u

        typedef struct { const char* file; int32_t line; } cd_trace_entry;

        static _Thread_local cd_trace_entry cd_trace[CD_TRACE_CAPACITY];

        """;

    public const string TracePush = """
        static void cd_trace_push(const char* file, int32_t line)
        {
            cd_trace[cd_trace_len % CD_TRACE_CAPACITY].file = file;
            cd_trace[cd_trace_len % CD_TRACE_CAPACITY].line = line;
            cd_trace_len++;
        }

        """;

    public const string TracePrint = """
        static void cd_trace_print(void)
        {
            if (cd_trace_len == 0)
            {
                return;
            }

            uint32_t first = cd_trace_len > CD_TRACE_CAPACITY ? cd_trace_len - CD_TRACE_CAPACITY : 0;
            cd_write_text("error return trace:\n");
            for (uint32_t i = first; i < cd_trace_len; i++)
            {
                cd_write_text("  ");
                cd_write_text(cd_trace[i % CD_TRACE_CAPACITY].file);
                cd_write_text(":");
                cd_write_int(cd_trace[i % CD_TRACE_CAPACITY].line);
                cd_write_text("\n");
            }
        }

        """;

    public const string LibcWriters = """
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

        """;

    public const string ReportError = """
        static void cd_report_error(const uint8_t* name, int32_t len)
        {
            cd_write_text("error: ");
            cd_libc_write_stderr(name, (size_t)len);
            cd_write_text("\n");
            cd_trace_print();
        }

        """;

    public const string PanicWithLibc = """
        static noreturn void cd_default_panic(const uint8_t* msg, int32_t msg_len, const char* file, int32_t line)
        {
            cd_write_text("panic: ");
            cd_libc_write_stderr(msg, (size_t)msg_len);
            cd_write_text("\n  at ");
            cd_write_text(file);
            cd_write_text(":");
            cd_write_int(line);
            cd_write_text("\n");
            @@TRACE_CALL@@
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
