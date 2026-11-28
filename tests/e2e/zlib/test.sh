set -euo pipefail

work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT

$CDC build zlib.csproj -o "$work/zlib"
set +e
"$work/zlib"
status=$?
set -e
test "$status" = 42

rm -rf obj bin
