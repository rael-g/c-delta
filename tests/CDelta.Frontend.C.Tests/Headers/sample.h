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
