set -euo pipefail

work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT
cc="${CC:-clang}"

$CDC build static/export-static.csproj
test -f static/bin/Debug/x86_64-linux-gnu/libgeometry.a
$cc consumer.c static/bin/Debug/x86_64-linux-gnu/libgeometry.a -o "$work/with-static"
set +e
"$work/with-static"
status=$?
set -e
test "$status" = 40

$CDC build shared/export-shared.csproj
test -f shared/bin/Debug/x86_64-linux-gnu/libgeometry.so
visible="$(nm -D --defined-only shared/bin/Debug/x86_64-linux-gnu/libgeometry.so | awk '{print $3}' | grep -E '^(geo_|Clamp|cd_)' | sort | tr '\n' ' ')"
test "$visible" = "Clamp geo_distance2 geo_twice "
$cc consumer.c -Lshared/bin/Debug/x86_64-linux-gnu -lgeometry -Wl,-rpath,"$PWD/shared/bin/Debug/x86_64-linux-gnu" -o "$work/with-shared"
set +e
"$work/with-shared"
status=$?
set -e
test "$status" = 40

rm -rf static/obj static/bin shared/obj shared/bin
