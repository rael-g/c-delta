set -euo pipefail

work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT

$CDC build sqlite.csproj -o "$work/sqlite"
set +e
"$work/sqlite"
status=$?
set -e
test "$status" = 42

rm -rf obj bin
