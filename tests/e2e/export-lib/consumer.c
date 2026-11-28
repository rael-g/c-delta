int geo_distance2(int ax, int ay, int bx, int by);
int Clamp(int value, int low, int high);
int geo_twice(int value);

int main(void)
{
    return geo_distance2(0, 0, 3, 4) + Clamp(99, 0, 10) + geo_twice(3) - 1;
}
