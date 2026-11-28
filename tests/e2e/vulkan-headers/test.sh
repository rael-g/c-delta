set -euo pipefail

work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT

$CDC build vulkan-headers.csproj -o "$work/vulkan"
"$work/vulkan"

rm -rf obj bin
