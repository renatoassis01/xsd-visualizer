#!/usr/bin/env bash
# Gera "XSD Visualizer.app" dentro de um .dmg e de um .zip para o macOS.
# Uso: packaging/package-macos.sh <osx-arm64|osx-x64> <versão>   (roda no macOS: usa codesign e hdiutil)
set -euo pipefail
rid=$1 version=$2
arch=${rid#osx-}
root=$(cd "$(dirname "$0")/.." && pwd)
work="$root/dist/work/$rid"
out="$root/dist/release"
app="$work/XSD Visualizer.app"
name="XsdVisualizer-$version-macos-$arch"

rm -rf "$work" && mkdir -p "$app/Contents/MacOS" "$app/Contents/Resources" "$out"
dotnet publish "$root/src/XsdVisualizer.App" -c Release -r "$rid" --self-contained true \
  -p:Version="$version" -o "$app/Contents/MacOS"
sed "s/@VERSION@/$version/g" "$root/packaging/macos/Info.plist" > "$app/Contents/Info.plist"
cp "$root/src/XsdVisualizer.App/Assets/app.icns" "$app/Contents/Resources/app.icns"
# Sem Developer ID: assinatura ad hoc, exigida para rodar no Apple Silicon.
codesign --force --deep --sign - "$app"

(cd "$work" && ditto -c -k --keepParent "XSD Visualizer.app" "$out/$name.zip")
staging="$work/dmg" && mkdir -p "$staging"
cp -R "$app" "$staging/" && ln -s /Applications "$staging/Applications"
hdiutil create -volname "XSD Visualizer" -srcfolder "$staging" -ov -format UDZO "$out/$name.dmg" >/dev/null
echo "$out/$name.dmg"
echo "$out/$name.zip"
