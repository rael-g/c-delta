#!/usr/bin/env bash
set -euo pipefail

packages=(qemu-user libc6-arm64-cross wine64 clang libclang-rt-dev gcc)
sysroot="${CDELTA_AARCH64_SYSROOT:-/usr/aarch64-linux-gnu}"

missing=()
for package in "${packages[@]}"; do
    if ! dpkg -s "$package" > /dev/null 2>&1; then
        missing+=("$package")
    fi
done

if [[ ${#missing[@]} -gt 0 ]]; then
    sudo apt-get update
    sudo apt-get install -y "${missing[@]}"
fi

fail=0

require_version() {
    local name="$1" minimum="$2" actual="$3"
    if [[ -z "$actual" ]] || [[ "$(printf '%s\n%s\n' "$minimum" "$actual" | sort -V | head -n1)" != "$minimum" ]]; then
        echo "missing or too old: $name (need $minimum or newer, found '${actual:-none}')" >&2
        fail=1
    fi
}

dotnet_version="$(dotnet --version 2>/dev/null | cut -d. -f1,2 || true)"
zig_version="$(zig version 2>/dev/null || true)"
clang_version="$(clang --version 2>/dev/null | grep -o 'version [0-9.]*' | head -n1 | cut -d' ' -f2 || true)"
gcc_version="$(gcc -dumpversion 2>/dev/null || true)"
wine_version="$(wine --version 2>/dev/null | grep -o '[0-9][0-9.]*' | head -n1 || true)"

require_version ".NET SDK" "10.0" "$dotnet_version"
require_version "zig" "0.15.1" "$zig_version"
require_version "clang" "18" "$clang_version"
require_version "gcc" "13" "$gcc_version"
require_version "wine" "9" "$wine_version"

if ! command -v qemu-aarch64 > /dev/null 2>&1; then
    echo "missing: qemu-aarch64" >&2
    fail=1
fi

if [[ ! -d "$sysroot" ]]; then
    echo "missing: aarch64 sysroot at $sysroot" >&2
    fail=1
fi

exit "$fail"
