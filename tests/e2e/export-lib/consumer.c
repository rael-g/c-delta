#include "geometry.h"

int main(void)
{
    return geo_distance2(0, 0, 3, 4) + Clamp(99, 0, 10) + geo_twice(3) - 1;
}
