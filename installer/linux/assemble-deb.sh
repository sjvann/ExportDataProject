#!/bin/bash
# 在 WSL 組出 exportdata 的 Debian 套件。參數：儲存庫根目錄（WSL 路徑）、版本。
set -euo pipefail
ROOT="$1"
VERSION="$2"
PUB="$ROOT/artifacts/publish/linux-x64"
OUT="$ROOT/artifacts/installers"
WORK="${HOME}/exportdata-deb-${VERSION}"

rm -rf "$WORK"
mkdir -p \
  "$WORK/opt/exportdata/web" \
  "$WORK/opt/exportdata/cli" \
  "$WORK/usr/bin" \
  "$WORK/usr/share/applications" \
  "$WORK/usr/share/icons/hicolor/256x256/apps" \
  "$WORK/DEBIAN" \
  "$OUT"

cp -a "$PUB/web/." "$WORK/opt/exportdata/web/"
cp -a "$PUB/cli/." "$WORK/opt/exportdata/cli/"
install -m 0755 "$ROOT/installer/launchers/linux/exportdata-workbench" "$WORK/usr/bin/exportdata-workbench"
install -m 0755 "$ROOT/installer/launchers/linux/exportdata" "$WORK/usr/bin/exportdata"
install -m 0644 "$ROOT/installer/launchers/linux/exportdata.desktop" "$WORK/usr/share/applications/exportdata.desktop"
install -m 0644 "$ROOT/artifacts/installer-assets/icon-256.png" "$WORK/usr/share/icons/hicolor/256x256/apps/exportdata.png"
install -m 0644 "$ROOT/artifacts/stage/linux-x64/使用說明.txt" "$WORK/opt/exportdata/使用說明.txt"
chmod 0755 "$WORK/opt/exportdata/web/ExportDataWeb" "$WORK/opt/exportdata/cli/ExportData"
sed -i 's/\r$//' \
  "$WORK/usr/bin/exportdata-workbench" \
  "$WORK/usr/bin/exportdata" \
  "$WORK/usr/share/applications/exportdata.desktop"

sed "s/__VERSION__/${VERSION}/g" "$ROOT/installer/linux/control" | sed 's/\r$//' > "$WORK/DEBIAN/control"
SIZE="$(du -sk "$WORK/opt" "$WORK/usr" | awk '{s+=$1} END {print s}')"
printf 'Installed-Size: %s\n' "$SIZE" >> "$WORK/DEBIAN/control"
sed 's/\r$//' "$ROOT/installer/linux/postinst" > "$WORK/DEBIAN/postinst"
sed 's/\r$//' "$ROOT/installer/linux/prerm" > "$WORK/DEBIAN/prerm"
chmod 0755 "$WORK/DEBIAN/postinst" "$WORK/DEBIAN/prerm"

DEB="$OUT/exportdata_${VERSION}_amd64.deb"
rm -f "$DEB"
if dpkg-deb --help 2>/dev/null | grep -q root-owner-group; then
  dpkg-deb --root-owner-group --build "$WORK" "$DEB"
else
  dpkg-deb --build "$WORK" "$DEB"
fi
dpkg-deb -I "$DEB"
echo "DEB $DEB"
