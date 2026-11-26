#include <stdint.h>
#include <stddef.h>
#include <stdbool.h>
#include <stdalign.h>
#include <stdnoreturn.h>

#if defined(__GNUC__) || defined(__clang__)
#define CD_UNUSED __attribute__((unused))
#else
#define CD_UNUSED
#endif

void cd_libc_write_stderr(const uint8_t* bytes, size_t length);
noreturn void cd_libc_abort(void);

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
    t4 = (b != ((int32_t)0));
    if (__builtin_expect(!(t4), 0)) cd_panic((const uint8_t*)"division by zero", 16, "Program.cs", 3);
    t5 = (a == ((int32_t)(-2147483647 - 1)));
    t6 = (b == ((int32_t)(-1)));
    t7 = (bool)(t5 & t6);
    t8 = !t7;
    if (__builtin_expect(!(t8), 0)) cd_panic((const uint8_t*)"integer overflow", 16, "Program.cs", 3);
    t2 = (int32_t)(a / b);
    t3 = (int32_t)(uint32_t)((uint32_t)t2 + (uint32_t)((int32_t)1));
    return t3;
}

static int32_t cd_Sample_Program_Main(void)
{
    int32_t t0;
    t0 = cd_Sample_Program_Divide(((int32_t)7), ((int32_t)2));
    return t0;
}

int main(int argc CD_UNUSED, char** argv CD_UNUSED)
{
    int32_t t2;
    int t3;
    t2 = cd_Sample_Program_Main();
    t3 = (int)t2;
    return t3;
}
