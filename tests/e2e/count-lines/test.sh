set -euo pipefail

work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT
sample="$(cd ../../../samples/count-lines && pwd)"
export WINEDEBUG=-all
sysroot="${CDELTA_AARCH64_SYSROOT:-/usr/aarch64-linux-gnu}"

check() {
    local program="$1"
    shift
    local runner=("$@")
    test "$("${runner[@]}" "$program" "$sample/three-lines.txt" | tr -d '\r')" = "3"
    set +e
    "${runner[@]}" "$program" "$work/missing.txt" > "$work/out" 2> "$work/err"
    local status=$?
    "${runner[@]}" "$program" > /dev/null 2>&1
    local usage=$?
    set -e
    test "$status" = 1
    tr -d '\r' < "$work/err" | head -n 1 | grep -qx "error: CannotOpen"
    grep -q "error return trace:" "$work/err"
    test "$usage" = 2
}

$CDC build "$sample/count-lines.cs" -o "$work/host"
check "$work/host"

if command -v qemu-aarch64 > /dev/null; then
    $CDC build "$sample/count-lines.cs" --target aarch64-linux-gnu -o "$work/arm"
    check "$work/arm" qemu-aarch64 -L "$sysroot"
fi

if command -v wine > /dev/null; then
    $CDC build "$sample/count-lines.cs" --target x86_64-windows-gnu -o "$work/windows.exe"
    check "$work/windows.exe" wine
fi

readelf -d "$work/host" | grep NEEDED | grep -qv 'libc.so\|ld-linux\|libpthread' && exit 1
true
