#!/usr/bin/env bash
# Preenche a cask do Homebrew com a versão e os checksums dos .dmg de um release.
# Uso: packaging/update-homebrew-cask.sh <versão> <SHA256SUMS.txt> <arquivo .rb de saída>
set -euo pipefail
version=$1 sums=$2 out=$3
root=$(cd "$(dirname "$0")/.." && pwd)
sha() { awk -v f="XsdVisualizer-$version-macos-$1.dmg" '$2 == f { print $1 }' "$sums"; }
arm=$(sha arm64) x64=$(sha x64)
[ -n "$arm" ] && [ -n "$x64" ] || { echo "checksums dos .dmg $version não encontrados em $sums" >&2; exit 1; }
mkdir -p "$(dirname "$out")"
sed -e "s/@VERSION@/$version/" -e "s/@SHA256_ARM64@/$arm/" -e "s/@SHA256_X64@/$x64/" \
  "$root/packaging/homebrew/xsd-visualizer.rb.template" > "$out"
echo "$out: $version"
