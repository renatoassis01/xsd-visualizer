#!/usr/bin/env bash
# Publica executáveis self-contained do XSD Visualizer para cada plataforma em ./dist/<rid>.
set -euo pipefail
cd "$(dirname "$0")"
rids=("${@:-win-x64 osx-arm64 osx-x64 linux-x64}")
for rid in ${rids[@]}; do
  dotnet publish src/XsdVisualizer.App -c Release -r "$rid" --self-contained true \
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o "dist/$rid"
done
