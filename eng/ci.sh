#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$root"

modes=(Debug ReleaseSafe ReleaseFast ReleaseSmall)
foreign_targets=(aarch64-linux-gnu x86_64-windows-gnu)

dotnet build CDelta.slnx
dotnet test CDelta.slnx

if [[ -x tests/conformance/run.sh ]]; then
    for mode in "${modes[@]}"; do
        tests/conformance/run.sh --mode "$mode"
    done
    for target in "${foreign_targets[@]}"; do
        tests/conformance/run.sh --target "$target"
    done
fi

if [[ -x tests/e2e/run.sh ]]; then
    tests/e2e/run.sh
fi
