#!/bin/bash
set -euo pipefail
python3 - "$1" <<'PY'
import pathlib, sys
data = pathlib.Path(sys.argv[1]).read_bytes()[:8]
magic = int.from_bytes(data[:4], "little")
cpu = int.from_bytes(data[4:8], "little")
if magic != 0xFEEDFACF or cpu != 0x01000007:
    sys.exit(f"不是 macOS x64 執行檔 magic={magic:#x} cpu={cpu:#x}")
print("osx binary ok")
PY
