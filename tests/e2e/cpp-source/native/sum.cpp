#include <numeric>
#include <vector>

#include "sum.h"

extern "C" int sum_squares(int count)
{
    std::vector<int> values;
    for (int i = 1; i <= count; i++)
    {
        values.push_back(i * i * SCALE);
    }

    return std::accumulate(values.begin(), values.end(), 0);
}
