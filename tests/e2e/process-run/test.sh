set -euo pipefail

work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT

$CDC build Program.cs -o "$work/run"
set +e
"$work/run"
ok=$?
"$work/run" extra > "$work/out" 2> "$work/err"
failed=$?
set -e
test "$ok" = 42
test "$failed" = 1
head -n 1 "$work/err" | grep -qx "error: Rejected"
grep -q "error return trace:" "$work/err"
