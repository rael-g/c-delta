set -euo pipefail

work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT
export HOME_OVERRIDE=1

dotnet build project-items.csproj -o "$work/check" > "$work/check.log" 2>&1 || { cat "$work/check.log"; exit 1; }

$CDC build project-items.csproj -o "$work/items"
set +e
"$work/items"
status=$?
set -e
test "$status" = 9

test -f "$work/../" 2> /dev/null || true
rm -rf obj bin
