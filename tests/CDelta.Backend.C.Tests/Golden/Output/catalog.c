#include <stdint.h>
#include <stddef.h>
#include <stdbool.h>
#include <stdalign.h>
#include <stdnoreturn.h>

typedef struct cd_T_Arrays cd_T_Arrays;
typedef struct cd_T_BitsFull cd_T_BitsFull;
typedef struct cd_T_BitsMixedTypes cd_T_BitsMixedTypes;
typedef struct cd_T_BitsPacked cd_T_BitsPacked;
typedef struct cd_T_BitsSigned cd_T_BitsSigned;
typedef struct cd_T_BitsSmall cd_T_BitsSmall;
typedef struct cd_T_BitsStraddle cd_T_BitsStraddle;
typedef struct cd_T_BitsWide cd_T_BitsWide;
typedef struct cd_T_BitsWithFields cd_T_BitsWithFields;
typedef struct cd_T_CTypes cd_T_CTypes;
typedef struct cd_T_Floats cd_T_Floats;
typedef struct cd_T_Nested cd_T_Nested;
typedef struct cd_T_NestedTwice cd_T_NestedTwice;
typedef struct cd_T_Packed1 cd_T_Packed1;
typedef struct cd_T_Packed2 cd_T_Packed2;
typedef struct cd_T_Packed4 cd_T_Packed4;
typedef struct cd_T_PointerAndBool cd_T_PointerAndBool;
typedef struct cd_T_PointerSized cd_T_PointerSized;
typedef struct cd_T_Scalars cd_T_Scalars;
typedef struct cd_T_Slices cd_T_Slices;
typedef struct cd_T_Wide cd_T_Wide;

typedef struct { int32_t* ptr; int32_t len; } cd_T_slice_int32_t;

typedef struct { int16_t e[3]; } cd_T_array_3_int16_t;
typedef struct { int64_t e[2]; } cd_T_array_2_int64_t;
struct cd_T_Arrays
{
    int8_t f0;
    cd_T_array_3_int16_t f1;
    uint8_t f2;
    cd_T_array_2_int64_t f3;
};

struct cd_T_BitsFull
{
    uint32_t f0 : 32;
    uint32_t f1 : 1;
};

struct cd_T_BitsMixedTypes
{
    uint8_t f0 : 3;
    uint32_t f1 : 5;
    uint8_t f2 : 2;
};

#pragma pack(push, 1)
struct cd_T_BitsPacked
{
    uint8_t f0;
    uint32_t f1 : 5;
    uint32_t f2 : 28;
    uint8_t f3;
};
#pragma pack(pop)

struct cd_T_BitsSigned
{
    int32_t f0 : 7;
    int8_t f1 : 3;
    int16_t f2 : 12;
    int64_t f3 : 1;
};

struct cd_T_BitsSmall
{
    uint8_t f0 : 3;
    uint8_t f1 : 5;
    uint8_t f2 : 1;
};

struct cd_T_BitsStraddle
{
    uint32_t f0 : 3;
    uint32_t f1 : 30;
    uint32_t f2 : 3;
};

struct cd_T_BitsWide
{
    uint64_t f0 : 40;
    uint64_t f1 : 30;
    uint8_t f2 : 2;
};

struct cd_T_BitsWithFields
{
    int8_t f0;
    uint16_t f1 : 4;
    uint16_t f2 : 9;
    int32_t f3;
    uint8_t f4 : 7;
};

struct cd_T_CTypes
{
    char f0;
    long f1;
    short f2;
    long long f3;
    int f4;
};

struct cd_T_Floats
{
    float f0;
    double f1;
    uint8_t f2;
    float f3;
};

struct cd_T_Scalars
{
    int8_t f0;
    int32_t f1;
    int8_t f2;
    int64_t f3;
};

struct cd_T_Nested
{
    int8_t f0;
    cd_T_Scalars f1;
    int16_t f2;
};

struct cd_T_NestedTwice
{
    uint8_t f0;
    cd_T_Nested f1;
    uint8_t f2;
};

#pragma pack(push, 1)
struct cd_T_Packed1
{
    int8_t f0;
    int32_t f1;
    int8_t f2;
    int64_t f3;
};
#pragma pack(pop)

#pragma pack(push, 2)
struct cd_T_Packed2
{
    int8_t f0;
    int32_t f1;
    int8_t f2;
    int64_t f3;
};
#pragma pack(pop)

#pragma pack(push, 4)
struct cd_T_Packed4
{
    uint8_t f0;
    double f1;
    uint16_t f2;
};
#pragma pack(pop)

struct cd_T_PointerAndBool
{
    bool f0;
    int16_t f1;
    void* f2;
    bool f3;
};

struct cd_T_PointerSized
{
    uint8_t f0;
    intptr_t f1;
    uint8_t f2;
    uintptr_t f3;
};

struct cd_T_Slices
{
    uint8_t f0;
    cd_T_slice_int32_t f1;
    uint8_t f2;
};

struct cd_T_Wide
{
    int8_t f0;
    __int128 f1;
    int8_t f2;
};

_Static_assert(sizeof(cd_T_Arrays) == 32, "layout of Arrays");
_Static_assert(_Alignof(cd_T_Arrays) == 8, "layout of Arrays");
_Static_assert(offsetof(cd_T_Arrays, f0) == 0, "layout of Arrays.f0");
_Static_assert(offsetof(cd_T_Arrays, f1) == 2, "layout of Arrays.f1");
_Static_assert(offsetof(cd_T_Arrays, f2) == 8, "layout of Arrays.f2");
_Static_assert(offsetof(cd_T_Arrays, f3) == 16, "layout of Arrays.f3");
_Static_assert(sizeof(cd_T_BitsFull) == 8, "layout of BitsFull");
_Static_assert(_Alignof(cd_T_BitsFull) == 4, "layout of BitsFull");
_Static_assert(sizeof(cd_T_BitsMixedTypes) == 4, "layout of BitsMixedTypes");
_Static_assert(_Alignof(cd_T_BitsMixedTypes) == 4, "layout of BitsMixedTypes");
_Static_assert(sizeof(cd_T_BitsPacked) == 7, "layout of BitsPacked");
_Static_assert(_Alignof(cd_T_BitsPacked) == 1, "layout of BitsPacked");
_Static_assert(offsetof(cd_T_BitsPacked, f0) == 0, "layout of BitsPacked.f0");
_Static_assert(offsetof(cd_T_BitsPacked, f3) == 6, "layout of BitsPacked.f3");
_Static_assert(sizeof(cd_T_BitsSigned) == 8, "layout of BitsSigned");
_Static_assert(_Alignof(cd_T_BitsSigned) == 8, "layout of BitsSigned");
_Static_assert(sizeof(cd_T_BitsSmall) == 2, "layout of BitsSmall");
_Static_assert(_Alignof(cd_T_BitsSmall) == 1, "layout of BitsSmall");
_Static_assert(sizeof(cd_T_BitsStraddle) == 12, "layout of BitsStraddle");
_Static_assert(_Alignof(cd_T_BitsStraddle) == 4, "layout of BitsStraddle");
_Static_assert(sizeof(cd_T_BitsWide) == 16, "layout of BitsWide");
_Static_assert(_Alignof(cd_T_BitsWide) == 8, "layout of BitsWide");
_Static_assert(sizeof(cd_T_BitsWithFields) == 12, "layout of BitsWithFields");
_Static_assert(_Alignof(cd_T_BitsWithFields) == 4, "layout of BitsWithFields");
_Static_assert(offsetof(cd_T_BitsWithFields, f0) == 0, "layout of BitsWithFields.f0");
_Static_assert(offsetof(cd_T_BitsWithFields, f3) == 4, "layout of BitsWithFields.f3");
_Static_assert(sizeof(cd_T_CTypes) == 40, "layout of CTypes");
_Static_assert(_Alignof(cd_T_CTypes) == 8, "layout of CTypes");
_Static_assert(offsetof(cd_T_CTypes, f0) == 0, "layout of CTypes.f0");
_Static_assert(offsetof(cd_T_CTypes, f1) == 8, "layout of CTypes.f1");
_Static_assert(offsetof(cd_T_CTypes, f2) == 16, "layout of CTypes.f2");
_Static_assert(offsetof(cd_T_CTypes, f3) == 24, "layout of CTypes.f3");
_Static_assert(offsetof(cd_T_CTypes, f4) == 32, "layout of CTypes.f4");
_Static_assert(sizeof(cd_T_Floats) == 24, "layout of Floats");
_Static_assert(_Alignof(cd_T_Floats) == 8, "layout of Floats");
_Static_assert(offsetof(cd_T_Floats, f0) == 0, "layout of Floats.f0");
_Static_assert(offsetof(cd_T_Floats, f1) == 8, "layout of Floats.f1");
_Static_assert(offsetof(cd_T_Floats, f2) == 16, "layout of Floats.f2");
_Static_assert(offsetof(cd_T_Floats, f3) == 20, "layout of Floats.f3");
_Static_assert(sizeof(cd_T_Nested) == 40, "layout of Nested");
_Static_assert(_Alignof(cd_T_Nested) == 8, "layout of Nested");
_Static_assert(offsetof(cd_T_Nested, f0) == 0, "layout of Nested.f0");
_Static_assert(offsetof(cd_T_Nested, f1) == 8, "layout of Nested.f1");
_Static_assert(offsetof(cd_T_Nested, f2) == 32, "layout of Nested.f2");
_Static_assert(sizeof(cd_T_NestedTwice) == 56, "layout of NestedTwice");
_Static_assert(_Alignof(cd_T_NestedTwice) == 8, "layout of NestedTwice");
_Static_assert(offsetof(cd_T_NestedTwice, f0) == 0, "layout of NestedTwice.f0");
_Static_assert(offsetof(cd_T_NestedTwice, f1) == 8, "layout of NestedTwice.f1");
_Static_assert(offsetof(cd_T_NestedTwice, f2) == 48, "layout of NestedTwice.f2");
_Static_assert(sizeof(cd_T_Packed1) == 14, "layout of Packed1");
_Static_assert(_Alignof(cd_T_Packed1) == 1, "layout of Packed1");
_Static_assert(offsetof(cd_T_Packed1, f0) == 0, "layout of Packed1.f0");
_Static_assert(offsetof(cd_T_Packed1, f1) == 1, "layout of Packed1.f1");
_Static_assert(offsetof(cd_T_Packed1, f2) == 5, "layout of Packed1.f2");
_Static_assert(offsetof(cd_T_Packed1, f3) == 6, "layout of Packed1.f3");
_Static_assert(sizeof(cd_T_Packed2) == 16, "layout of Packed2");
_Static_assert(_Alignof(cd_T_Packed2) == 2, "layout of Packed2");
_Static_assert(offsetof(cd_T_Packed2, f0) == 0, "layout of Packed2.f0");
_Static_assert(offsetof(cd_T_Packed2, f1) == 2, "layout of Packed2.f1");
_Static_assert(offsetof(cd_T_Packed2, f2) == 6, "layout of Packed2.f2");
_Static_assert(offsetof(cd_T_Packed2, f3) == 8, "layout of Packed2.f3");
_Static_assert(sizeof(cd_T_Packed4) == 16, "layout of Packed4");
_Static_assert(_Alignof(cd_T_Packed4) == 4, "layout of Packed4");
_Static_assert(offsetof(cd_T_Packed4, f0) == 0, "layout of Packed4.f0");
_Static_assert(offsetof(cd_T_Packed4, f1) == 4, "layout of Packed4.f1");
_Static_assert(offsetof(cd_T_Packed4, f2) == 12, "layout of Packed4.f2");
_Static_assert(sizeof(cd_T_PointerAndBool) == 24, "layout of PointerAndBool");
_Static_assert(_Alignof(cd_T_PointerAndBool) == 8, "layout of PointerAndBool");
_Static_assert(offsetof(cd_T_PointerAndBool, f0) == 0, "layout of PointerAndBool.f0");
_Static_assert(offsetof(cd_T_PointerAndBool, f1) == 2, "layout of PointerAndBool.f1");
_Static_assert(offsetof(cd_T_PointerAndBool, f2) == 8, "layout of PointerAndBool.f2");
_Static_assert(offsetof(cd_T_PointerAndBool, f3) == 16, "layout of PointerAndBool.f3");
_Static_assert(sizeof(cd_T_PointerSized) == 32, "layout of PointerSized");
_Static_assert(_Alignof(cd_T_PointerSized) == 8, "layout of PointerSized");
_Static_assert(offsetof(cd_T_PointerSized, f0) == 0, "layout of PointerSized.f0");
_Static_assert(offsetof(cd_T_PointerSized, f1) == 8, "layout of PointerSized.f1");
_Static_assert(offsetof(cd_T_PointerSized, f2) == 16, "layout of PointerSized.f2");
_Static_assert(offsetof(cd_T_PointerSized, f3) == 24, "layout of PointerSized.f3");
_Static_assert(sizeof(cd_T_Scalars) == 24, "layout of Scalars");
_Static_assert(_Alignof(cd_T_Scalars) == 8, "layout of Scalars");
_Static_assert(offsetof(cd_T_Scalars, f0) == 0, "layout of Scalars.f0");
_Static_assert(offsetof(cd_T_Scalars, f1) == 4, "layout of Scalars.f1");
_Static_assert(offsetof(cd_T_Scalars, f2) == 8, "layout of Scalars.f2");
_Static_assert(offsetof(cd_T_Scalars, f3) == 16, "layout of Scalars.f3");
_Static_assert(sizeof(cd_T_Slices) == 32, "layout of Slices");
_Static_assert(_Alignof(cd_T_Slices) == 8, "layout of Slices");
_Static_assert(offsetof(cd_T_Slices, f0) == 0, "layout of Slices.f0");
_Static_assert(offsetof(cd_T_Slices, f1) == 8, "layout of Slices.f1");
_Static_assert(offsetof(cd_T_Slices, f2) == 24, "layout of Slices.f2");
_Static_assert(sizeof(cd_T_Wide) == 48, "layout of Wide");
_Static_assert(_Alignof(cd_T_Wide) == 16, "layout of Wide");
_Static_assert(offsetof(cd_T_Wide, f0) == 0, "layout of Wide.f0");
_Static_assert(offsetof(cd_T_Wide, f1) == 16, "layout of Wide.f1");
_Static_assert(offsetof(cd_T_Wide, f2) == 32, "layout of Wide.f2");

