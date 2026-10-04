#!/bin/bash
# 在 WSL 起動已發布的 Linux 工作台與命令列，確認首頁、樣式表與 SQLite。
set -euo pipefail
ROOT="$1"
PUB="$ROOT/artifacts/publish/linux-x64"
SMOKE="${HOME}/exportdata-smoke"
rm -rf "$SMOKE"
mkdir -p "$SMOKE/web" "$SMOKE/cli"
cp -a "$PUB/web/." "$SMOKE/web/"
cp -a "$PUB/cli/." "$SMOKE/cli/"
chmod 0755 "$SMOKE/web/ExportDataWeb" "$SMOKE/cli/ExportData"

URL="http://127.0.0.1:5198"
LOG="$SMOKE/web.log"
cd "$SMOKE/web"
ASPNETCORE_ENVIRONMENT=Production ASPNETCORE_URLS="$URL" DOTNET_NOLOGO=1 \
  ./ExportDataWeb >"$LOG" 2>&1 &
PID=$!
cleanup() { kill "$PID" 2>/dev/null || true; wait "$PID" 2>/dev/null || true; }
trap cleanup EXIT

python3 - "$URL" "$LOG" <<'PY'
import re, sys, time, urllib.request
url, log = sys.argv[1], sys.argv[2]
last = ""
for _ in range(40):
    try:
        with urllib.request.urlopen(url, timeout=3) as resp:
            body = resp.read().decode("utf-8", "replace")
            if resp.status != 200 or "析庫" not in body:
                raise RuntimeError(f"status={resp.status}")
            css_match = re.search(r'href="([^"]*styles\.css[^"]*)"', body)
            icon_match = re.search(r'href="([^"]*app-icon[^"]*)"', body)
            if not css_match or not icon_match:
                raise SystemExit("首頁沒有樣式表或圖示連結")
            def absolute(href):
                return href if href.startswith("http") else url + href
            with urllib.request.urlopen(absolute(css_match.group(1)), timeout=5) as css_resp:
                if css_resp.status != 200:
                    raise SystemExit(f"樣式表 {css_resp.status}")
            with urllib.request.urlopen(absolute(icon_match.group(1)), timeout=5) as icon_resp:
                if icon_resp.status != 200:
                    raise SystemExit(f"圖示 {icon_resp.status}")
            print("linux web ok")
            sys.exit(0)
    except SystemExit:
        raise
    except Exception as ex:
        last = str(ex)
        time.sleep(1)
print(last)
print(pathlib_read(log) if False else open(log, encoding="utf-8", errors="replace").read()[-4000:])
sys.exit(1)
PY

cd "$SMOKE/cli"
set +e
./ExportData --test >"$SMOKE/cli.log" 2>&1
set -e
if grep -q "SQLite 測試失敗" "$SMOKE/cli.log"; then
  cat "$SMOKE/cli.log"
  exit 1
fi
grep -q "SQLite 測試完成" "$SMOKE/cli.log"
echo "linux sqlite ok"
