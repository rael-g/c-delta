set -euo pipefail

work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT

$CDC build cpp-source.csproj -o "$work/sum"
set +e
"$work/sum"
status=$?
set -e
test "$status" = 30

rm -rf obj bin
