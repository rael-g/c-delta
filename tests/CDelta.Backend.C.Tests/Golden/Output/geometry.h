#ifndef GEOMETRY_H
#define GEOMETRY_H

#include <stdint.h>
#include <stddef.h>
#include <stdbool.h>

#ifdef __cplusplus
extern "C" {
#endif

typedef int32_t (*cd_T_fn_1)(int32_t);

int32_t geo_add(int32_t, int32_t);
bool geo_apply(cd_T_fn_1, const uint8_t*, int64_t);

#ifdef __cplusplus
}
#endif

#endif
