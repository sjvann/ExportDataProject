#!/bin/bash
# 在使用者目錄準備 cpio、gcc 與 mkbom。不需要 sudo。
set -euo pipefail
PREFIX="${HOME}/.local/exportdata-pkgtools"
mkdir -p "$PREFIX/bin" "$PREFIX/debs" "$PREFIX/src"
export PATH="${PREFIX}/bin:${PREFIX}/usr/bin:${PATH}"
export LD_LIBRARY_PATH="${PREFIX}/usr/lib/x86_64-linux-gnu:${PREFIX}/lib/x86_64-linux-gnu${LD_LIBRARY_PATH:+:${LD_LIBRARY_PATH}}"

if [[ -x "${PREFIX}/bin/mkbom" && -x "${PREFIX}/usr/bin/cpio" ]]; then
  echo "pkg tools ready: ${PREFIX}"
  exit 0
fi

echo "下載打包工具（第一次會比較久）..."
mapfile -t pkgs < <(apt-cache depends --recurse --no-recommends --no-suggests --no-conflicts --no-breaks --no-replaces --no-enhances \
  cpio g++ make zlib1g-dev \
  | grep -E '^[a-zA-Z0-9]' | sort -u)
cd "$PREFIX/debs"
for pkg in "${pkgs[@]}"; do
  if compgen -G "${pkg}_*.deb" > /dev/null; then
    continue
  fi
  apt-get download "$pkg" || true
  if compgen -G "${pkg}_*.deb" > /dev/null; then
    continue
  fi
  python3 - "$pkg" "$PREFIX/debs" <<'PY'
import gzip, os, sys, urllib.request
pkg, dest = sys.argv[1], sys.argv[2]
indexes = [
    "http://archive.ubuntu.com/ubuntu/dists/noble-updates/main/binary-amd64/Packages.gz",
    "http://archive.ubuntu.com/ubuntu/dists/noble/main/binary-amd64/Packages.gz",
]
for index in indexes:
    data = gzip.decompress(urllib.request.urlopen(index, timeout=60).read()).decode("utf-8", "replace")
    current = None
    filename = None
    for line in data.splitlines() + [""]:
        if line.startswith("Package: "):
            current = line.split(": ", 1)[1]
            filename = None
        elif line.startswith("Filename: ") and current == pkg:
            filename = line.split(": ", 1)[1]
        elif line == "" and current == pkg and filename:
            url = "http://archive.ubuntu.com/ubuntu/" + filename
            target = os.path.join(dest, os.path.basename(filename))
            print(f"改從索引下載 {pkg}")
            urllib.request.urlretrieve(url, target)
            sys.exit(0)
print(f"略過無法下載的套件：{pkg}", file=sys.stderr)
sys.exit(0)
PY
done
for deb in "$PREFIX/debs/"*.deb; do
  dpkg-deb -x "$deb" "$PREFIX"
done

if [[ ! -e "${PREFIX}/usr/bin/gcc" ]]; then
  gcc_bin="$(find "${PREFIX}/usr/bin" -maxdepth 1 -type f -name 'gcc-*' | sort -V | tail -1)"
  ln -sfn "$gcc_bin" "${PREFIX}/usr/bin/gcc"
fi
if [[ ! -e "${PREFIX}/usr/bin/g++" ]]; then
  gxx_bin="$(find "${PREFIX}/usr/bin" -maxdepth 1 -type f -name 'g++-*' | sort -V | tail -1)"
  ln -sfn "$gxx_bin" "${PREFIX}/usr/bin/g++"
fi
if [[ ! -x "${PREFIX}/usr/bin/cpio" ]]; then
  echo "cpio 沒有出現在 ${PREFIX}/usr/bin" >&2
  exit 1
fi

if [[ ! -d "${PREFIX}/src/bomutils/.git" ]]; then
  git clone --depth 1 https://github.com/hogliux/bomutils.git "${PREFIX}/src/bomutils"
fi
export LIBRARY_PATH="${PREFIX}/usr/lib/x86_64-linux-gnu:/usr/lib/x86_64-linux-gnu${LIBRARY_PATH:+:${LIBRARY_PATH}}"
# 把 libc 標頭放在 libstdc++ 之後，否則 include_next 找不到 stdlib.h。
write_cc_wrapper() {
  local name="$1"
  local real="$2"
  cat > "${PREFIX}/bin/${name}" <<EOF
#!/bin/bash
export LD_LIBRARY_PATH="${PREFIX}/usr/lib/x86_64-linux-gnu:\${LD_LIBRARY_PATH:-}"
exec "${PREFIX}/usr/bin/${real}" --sysroot="${PREFIX}" "\$@"
EOF
  chmod 0755 "${PREFIX}/bin/${name}"
}
ln -sfn usr/lib "${PREFIX}/lib"
ln -sfn usr/lib64 "${PREFIX}/lib64"
write_cc_wrapper gcc x86_64-linux-gnu-gcc-13
write_cc_wrapper g++ x86_64-linux-gnu-g++-13
write_cc_wrapper cc x86_64-linux-gnu-gcc-13
write_cc_wrapper c++ x86_64-linux-gnu-g++-13
make -C "${PREFIX}/src/bomutils" -j"$(nproc)"
cp -f "${PREFIX}/src/bomutils/build/bin/mkbom" "${PREFIX}/bin/mkbom"
chmod 0755 "${PREFIX}/bin/mkbom"
echo "pkg tools ready: ${PREFIX}"
