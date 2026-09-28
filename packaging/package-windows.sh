#!/usr/bin/env bash
# Gera o instalador (Inno Setup) e o .zip portátil para o Windows.
# Uso (Git Bash no Windows): packaging/package-windows.sh <win-x64|win-arm64> <versão>
set -euo pipefail
rid=$1 version=$2
arch=${rid#win-}
root=$(cd "$(dirname "$0")/.." && pwd)
work="$root/dist/work/$rid"
out="$root/dist/release"
publish="$work/publish"

rm -rf "$work" && mkdir -p "$publish" "$out"
dotnet publish "$root/src/XsdVisualizer.App" -c Release -r "$rid" --self-contained true \
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:Version="$version" -o "$publish"
rm -f "$publish"/*.pdb

(cd "$publish" && 7z a -tzip -bso0 "$out/XsdVisualizer-$version-windows-$arch.zip" .)

iscc="${ISCC:-/c/Program Files (x86)/Inno Setup 6/ISCC.exe}"
"$iscc" //Q //DAppVersion="$version" //DArch="$arch" \
  //DSourceDir="$(cygpath -w "$publish")" //DOutputDir="$(cygpath -w "$out")" \
  "$(cygpath -w "$root/packaging/windows/installer.iss")"
ls -la "$out"
