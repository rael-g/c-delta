set -euo pipefail

work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT
sample="$(cd ../../../samples/list-arena && pwd)"

$CDC build "$sample/list-arena.cs" -o "$work/host"
set +e
"$work/host"
status=$?
set -e
test "$status" = 42

$CDC build "$sample/list-arena.cs" --mode ReleaseSafe -o "$work/safe"
set +e
"$work/safe"
status=$?
set -e
test "$status" = 42
