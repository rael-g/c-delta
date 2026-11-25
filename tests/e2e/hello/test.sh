set -euo pipefail

work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT

$CDC build Program.cs -o "$work/hello"
test "$("$work/hello")" = "hello"

$CDC run Program.cs --mode ReleaseFast -o "$work/hello-fast" | grep -qx hello
