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

    public const string LibcIncludes = """
        #include <stdio.h>
        #include <stdlib.h>

        """;

    public const string PanicWithLibc = """
        static noreturn void cd_default_panic(const uint8_t* msg, int32_t msg_len, const char* file, int32_t line)
        {
            fputs("panic: ", stderr);
            fwrite(msg, 1, (size_t)msg_len, stderr);
            fprintf(stderr, "\n  at %s:%d\n", file, (int)line);
            abort();
        }

        static noreturn void cd_panic(const uint8_t* msg, int32_t msg_len, const char* file, int32_t line)
        {
            cd_default_panic(msg, msg_len, file, line);
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

        static noreturn void cd_panic(const uint8_t* msg, int32_t msg_len, const char* file, int32_t line)
        {
            cd_default_panic(msg, msg_len, file, line);
        }

        """;
}
