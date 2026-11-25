#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
export CDC="dotnet $root/artifacts/bin/CDelta.Cli/debug/cdc.dll"

if [[ $# -gt 0 ]]; then
    cases=("$@")
else
    cases=()
    for directory in "$root"/tests/e2e/*/; do
        cases+=("$(basename "$directory")")
    done
fi

failed=0
for name in "${cases[@]}"; do
    if (cd "$root/tests/e2e/$name" && bash test.sh); then
        echo "ok   $name"
    else
        echo "FAIL $name"
        failed=1
    fi
done

exit "$failed"
