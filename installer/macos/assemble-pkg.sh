#!/bin/bash
# 在 WSL 組出 macOS flat package。參數：儲存庫根目錄（WSL 路徑）、版本。
set -euo pipefail
ROOT="$1"
VERSION="$2"
PUB="$ROOT/artifacts/publish/osx-x64"
OUT="$ROOT/artifacts/installers"
PREFIX="${HOME}/.local/exportdata-pkgtools"
export PATH="${PREFIX}/usr/bin:${PREFIX}/bin:${PATH}"
BOOT="$(mktemp)"
sed 's/\r$//' "$ROOT/installer/macos/bootstrap-pkg-tools.sh" > "$BOOT"
bash "$BOOT"
rm -f "$BOOT"

BUILD="${HOME}/exportdata-pkg-${VERSION}"
APP="${BUILD}/root/Applications/析庫.app"
rm -rf "$BUILD"
mkdir -p \
  "$APP/Contents/MacOS" \
  "$APP/Contents/Resources/web" \
  "$APP/Contents/Resources/cli" \
  "$BUILD/flat/base.pkg" \
  "$BUILD/flat/Resources/zh_TW.lproj" \
  "$BUILD/scripts" \
  "$OUT"

cp -a "$PUB/web/." "$APP/Contents/Resources/web/"
cp -a "$PUB/cli/." "$APP/Contents/Resources/cli/"
sed "s/__VERSION__/${VERSION}/g" "$ROOT/installer/macos/Info.plist" | sed 's/\r$//' > "$APP/Contents/Info.plist"
sed 's/\r$//' "$ROOT/installer/launchers/macos/exportdata" > "$APP/Contents/MacOS/exportdata"
printf 'APPL????' > "$APP/Contents/PkgInfo"
cp -f "$ROOT/artifacts/installer-assets/AppIcon.icns" "$APP/Contents/Resources/AppIcon.icns"
cp -f "$ROOT/artifacts/stage/osx-x64/使用說明.txt" "$APP/Contents/Resources/使用說明.txt"
chmod 0755 "$APP/Contents/MacOS/exportdata" \
  "$APP/Contents/Resources/web/ExportDataWeb" \
  "$APP/Contents/Resources/cli/ExportData"
sed 's/\r$//' "$ROOT/installer/macos/postinstall" > "$BUILD/scripts/postinstall"
chmod 0755 "$BUILD/scripts/postinstall"

FILE_COUNT="$(find "$BUILD/root" | wc -l | tr -d ' ')"
KBYTES="$(du -sk "$BUILD/root" | awk '{print $1}')"

cat > "$BUILD/flat/base.pkg/PackageInfo" <<EOF
<?xml version="1.0" encoding="utf-8"?>
<pkg-info format-version="2" identifier="com.github.sjvann.ExportData.pkg" version="${VERSION}" install-location="/" auth="root">
  <payload installKBytes="${KBYTES}" numberOfFiles="${FILE_COUNT}"/>
  <scripts>
    <postinstall file="./postinstall"/>
  </scripts>
  <bundle-version>
    <bundle id="com.github.sjvann.ExportData" CFBundleIdentifier="com.github.sjvann.ExportData" path="./Applications/析庫.app" CFBundleVersion="${VERSION}"/>
  </bundle-version>
</pkg-info>
EOF

cat > "$BUILD/flat/Distribution" <<EOF
<?xml version="1.0" encoding="utf-8"?>
<installer-script minSpecVersion="1.000000" authoringTool="ExportData" authoringToolVersion="${VERSION}">
  <title>析庫 ${VERSION}</title>
  <options customize="never" require-scripts="false" rootVolumeOnly="true"/>
  <domains enable_localSystem="true"/>
  <welcome file="Welcome.html" mime-type="text/html" lang="zh_TW"/>
  <choices-outline>
    <line choice="default"/>
  </choices-outline>
  <choice id="default" title="析庫" description="本機舊系統資料庫工具">
    <pkg-ref id="com.github.sjvann.ExportData.pkg"/>
  </choice>
  <pkg-ref id="com.github.sjvann.ExportData.pkg" version="${VERSION}" auth="root" installKBytes="${KBYTES}">#base.pkg</pkg-ref>
</installer-script>
EOF

cat > "$BUILD/flat/Resources/zh_TW.lproj/Welcome.html" <<EOF
<!DOCTYPE html>
<html lang="zh-Hant">
<head><meta charset="utf-8"><title>析庫</title></head>
<body>
<p>這會把析庫裝到「應用程式」，並在終端機提供 <code>exportdata</code> 命令。</p>
<p>安裝後從「應用程式」開啟「析庫」。工作台位址是 http://127.0.0.1:5107 。</p>
<p>此套件為 macOS x64。Apple 晶片請先安裝 Rosetta：<code>softwareupdate --install-rosetta --agree-to-license</code></p>
</body>
</html>
EOF

( cd "$BUILD/root" && find . | LC_ALL=C sort | cpio -o --format odc --owner 0:80 | gzip -c ) > "$BUILD/flat/base.pkg/Payload"
( cd "$BUILD/scripts" && find . | LC_ALL=C sort | cpio -o --format odc --owner 0:80 | gzip -c ) > "$BUILD/flat/base.pkg/Scripts"
mkbom -u 0 -g 80 "$BUILD/root" "$BUILD/flat/base.pkg/Bom"

PKG="$OUT/ExportData-${VERSION}-osx-x64.pkg"
rm -f "$PKG"
python3 "$ROOT/installer/macos/make_xar.py" "$BUILD/flat" "$PKG"
python3 - <<PY
import pathlib, sys
data = pathlib.Path("$PKG").read_bytes()[:4]
if data != b"xar!":
    sys.exit("pkg 標頭不是 xar")
print("PKG", "$PKG", pathlib.Path("$PKG").stat().st_size)
PY
