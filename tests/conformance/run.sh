#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"

while [[ $# -gt 0 ]]; do
    case "$1" in
        --mode) export CDELTA_MODE="$2"; shift 2 ;;
        --target) export CDELTA_TARGET="$2"; shift 2 ;;
        --filter) export CDELTA_FILTER="$2"; shift 2 ;;
        *) echo "usage: run.sh [--mode m] [--target t] [--filter text]" >&2; exit 2 ;;
    esac
done

cd "$root"
dotnet test tests/CDelta.Conformance.Tests
