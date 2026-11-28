#define SAMPLE_OK 0
#define SAMPLE_NAME "sample"
#define SAMPLE_RATIO 0.5
#define SAMPLE_LETTER 'x'

typedef struct sample sample;
typedef struct { int x; int y; } point;

struct pair { int a; long b; unsigned char tag[4]; };
union number { int i; float f; };

enum mode { MODE_A, MODE_B = 5 };

typedef int (*callback)(int, void*);

extern int sample_count;
int sample_open(const char* path, sample** out);
int sample_log(const char* format, ...);
point sample_origin(void);
static inline int sample_twice(int v) { return v * 2; }

#define SAMPLE_REGISTER ((volatile unsigned int *)0x40021000)
#define SAMPLE_CONTROL ((volatile unsigned int *)(0x40021000 + 0x18))
#define SAMPLE_BUFFER ((struct pair *)0x20000000)
extern int sample_global;
#define SAMPLE_GLOBAL_ADDRESS (&sample_global)

struct wide { long double value; };
int sample_wide(struct wide* w);
