set -euo pipefail

work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT

$CDC build asm-source.csproj -o "$work/seven"
set +e
"$work/seven"
status=$?
set -e
test "$status" = 42

rm -rf obj bin
