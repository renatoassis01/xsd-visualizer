#!/usr/bin/env bash
# Gera AppImage, .deb e .tar.gz para o Linux.
# Uso: packaging/package-linux.sh <linux-x64|linux-arm64> <versão>
# Roda num Linux da mesma arquitetura (o appimagetool embute o runtime da máquina).
set -euo pipefail
rid=$1 version=$2
case "$rid" in
  linux-x64) arch=x64 deb_arch=amd64 appimage_arch=x86_64 ;;
  linux-arm64) arch=arm64 deb_arch=arm64 appimage_arch=aarch64 ;;
  *) echo "rid não suportado: $rid" >&2; exit 1 ;;
esac
for tool in dpkg-deb file curl; do
  command -v "$tool" >/dev/null || { echo "falta o comando $tool (apt-get install dpkg file curl)" >&2; exit 1; }
done
root=$(cd "$(dirname "$0")/.." && pwd)
work="$root/dist/work/$rid"
out="$root/dist/release"
name="XsdVisualizer-$version-linux-$arch"
publish="$work/publish"

rm -rf "$work" && mkdir -p "$publish" "$out"
dotnet publish "$root/src/XsdVisualizer.App" -c Release -r "$rid" --self-contained true \
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:Version="$version" -o "$publish"
rm -f "$publish"/*.pdb

# .tar.gz: o executável solto.
tar -czf "$out/$name.tar.gz" -C "$publish" .

# .deb: /opt/xsd-visualizer, comando xsd-visualizer, atalho e ícone.
deb="$work/deb"
mkdir -p "$deb/DEBIAN" "$deb/opt/xsd-visualizer" "$deb/usr/bin" \
  "$deb/usr/share/applications" "$deb/usr/share/icons/hicolor/256x256/apps"
cp -R "$publish"/. "$deb/opt/xsd-visualizer/"
ln -s /opt/xsd-visualizer/XsdVisualizer "$deb/usr/bin/xsd-visualizer"
cp "$root/packaging/linux/xsd-visualizer.desktop" "$deb/usr/share/applications/"
cp "$root/packaging/linux/xsd-visualizer.png" "$deb/usr/share/icons/hicolor/256x256/apps/"
cat > "$deb/DEBIAN/control" <<CONTROL
Package: xsd-visualizer
Version: $version
Architecture: $deb_arch
Maintainer: Renato Assis <1714391+renatoassis01@users.noreply.github.com>
Homepage: https://github.com/renatoassis01/xsd-visualizer
Section: devel
Priority: optional
Depends: libc6, libfontconfig1, libx11-6, libice6, libsm6, libicu78 | libicu77 | libicu76 | libicu75 | libicu74 | libicu73 | libicu72 | libicu71 | libicu70
Description: Explore XSD and WSDL sets and generate valid sample XML
 Desktop app to browse XSD 1.0 and WSDL 1.1 schema sets, generate valid sample
 XML and SOAP envelopes (Minimal, Maximal and Coverage Set), validate existing
 documents and compare two versions of a schema set.
CONTROL
dpkg-deb --build --root-owner-group "$deb" "$out/$name.deb"

# AppImage.
appdir="$work/XSD_Visualizer.AppDir"
mkdir -p "$appdir/usr/bin"
cp -R "$publish"/. "$appdir/usr/bin/"
cp "$root/packaging/linux/xsd-visualizer.desktop" "$root/packaging/linux/xsd-visualizer.png" "$appdir/"
ln -s xsd-visualizer.png "$appdir/.DirIcon"
cat > "$appdir/AppRun" <<'APPRUN'
#!/bin/sh
here="$(dirname "$(readlink -f "$0")")"
exec "$here/usr/bin/XsdVisualizer" "$@"
APPRUN
chmod +x "$appdir/AppRun"
tool="$work/appimagetool"
curl -fsSL -o "$tool" "https://github.com/AppImage/appimagetool/releases/download/continuous/appimagetool-$appimage_arch.AppImage"
chmod +x "$tool"
ARCH=$appimage_arch APPIMAGE_EXTRACT_AND_RUN=1 "$tool" --no-appstream "$appdir" "$out/$name.AppImage"

ls -la "$out"
