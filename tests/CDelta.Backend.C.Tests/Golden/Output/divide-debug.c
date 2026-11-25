#include <stdint.h>
#include <stddef.h>
#include <stdbool.h>
#include <stdalign.h>
#include <stdnoreturn.h>
#include <stdio.h>
#include <stdlib.h>

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

static int32_t cd_Sample_Program_Divide(int32_t, int32_t);
static int32_t cd_Sample_Program_Main(void);
int main(int, char**);

static int32_t cd_Sample_Program_Divide(int32_t a, int32_t b)
{
    int32_t t2;
    int32_t t3;
    bool t4;
    bool t5;
    bool t6;
    bool t7;
    bool t8;
#line 3 "$ROOT/Program.cs"
    t4 = (b != ((int32_t)0));
#line 3 "$ROOT/Program.cs"
    if (__builtin_expect(!(t4), 0)) cd_panic((const uint8_t*)"division by zero", 16, "Program.cs", 3);
#line 3 "$ROOT/Program.cs"
    t5 = (a == ((int32_t)(-2147483647 - 1)));
#line 3 "$ROOT/Program.cs"
    t6 = (b == ((int32_t)(-1)));
#line 3 "$ROOT/Program.cs"
    t7 = (bool)(t5 & t6);
#line 3 "$ROOT/Program.cs"
    t8 = !t7;
#line 3 "$ROOT/Program.cs"
    if (__builtin_expect(!(t8), 0)) cd_panic((const uint8_t*)"integer overflow", 16, "Program.cs", 3);
#line 3 "$ROOT/Program.cs"
    t2 = (int32_t)(a / b);
#line 3 "$ROOT/Program.cs"
    if (__builtin_expect(__builtin_add_overflow(t2, ((int32_t)1), &t3), 0)) cd_panic((const uint8_t*)"integer overflow", 16, "Program.cs", 3);
#line 3 "$ROOT/Program.cs"
    return t3;
}

static int32_t cd_Sample_Program_Main(void)
{
    int32_t t0;
#line 3 "$ROOT/Program.cs"
    t0 = cd_Sample_Program_Divide(((int32_t)7), ((int32_t)2));
#line 3 "$ROOT/Program.cs"
    return t0;
}

int main(int argc, char** argv)
{
    int32_t t2;
    int t3;
    t2 = cd_Sample_Program_Main();
    t3 = (int)t2;
    return t3;
}
