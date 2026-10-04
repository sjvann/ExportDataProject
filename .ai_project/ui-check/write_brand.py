from pathlib import Path

name = "\u6790\u5eab"
tagline = "\u820a\u7cfb\u7d71\u8cc7\u6599\u5eab\u89e3\u6790"
root = Path(r"e:\sjvann\ExportDataProject\ExportDataWeb\wwwroot\brand")

files = {
    "app-icon.svg": f"""<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 32 32" role="img">
  <title>{name}</title>
  <desc>App icon. Navy tile, white table grid, green confirmed cell. For favicon, app icon, and small marks on light backgrounds.</desc>
  <rect width="32" height="32" rx="8" fill="#1E3A5F"/>
  <path fill="none" stroke="#F8FAFC" stroke-width="1.75" stroke-linecap="square" d="M8 12h16M8 20h16M16 8v12"/>
  <rect x="17.15" y="21.15" width="6.7" height="6.7" rx="1.4" fill="#059669"/>
</svg>
""",
    "mark.svg": f"""<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 32 32" fill="none" role="img">
  <title>{name}</title>
  <desc>Light-background mark. Transparent, navy grid, green confirmed cell. For documents and white surfaces.</desc>
  <rect x="2.25" y="2.25" width="27.5" height="27.5" rx="6" stroke="#1E3A5F" stroke-width="1.75"/>
  <path stroke="#1E3A5F" stroke-width="1.75" stroke-linecap="square" d="M8 12h16M8 20h16M16 8v12"/>
  <rect x="17.15" y="21.15" width="6.7" height="6.7" rx="1.4" fill="#059669"/>
</svg>
""",
    "mark-inverse.svg": f"""<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 32 32" fill="none" role="img">
  <title>{name}</title>
  <desc>Dark-background mark. Transparent, white grid, green confirmed cell.</desc>
  <rect x="2.25" y="2.25" width="27.5" height="27.5" rx="6" stroke="#F8FAFC" stroke-width="1.75"/>
  <path stroke="#F8FAFC" stroke-width="1.75" stroke-linecap="square" d="M8 12h16M8 20h16M16 8v12"/>
  <rect x="17.15" y="21.15" width="6.7" height="6.7" rx="1.4" fill="#059669"/>
</svg>
""",
    "mark-mono.svg": f"""<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 32 32" fill="none" role="img">
  <title>{name}</title>
  <desc>Single-color mark using currentColor. For stamps, watermarks, and one-color print.</desc>
  <rect x="2.25" y="2.25" width="27.5" height="27.5" rx="6" stroke="currentColor" stroke-width="1.75"/>
  <path stroke="currentColor" stroke-width="1.75" stroke-linecap="square" d="M8 12h16M8 20h16M16 8v12"/>
  <rect x="17.15" y="21.15" width="6.7" height="6.7" rx="1.4" fill="currentColor"/>
</svg>
""",
    "lockup.svg": f"""<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 248 48" role="img">
  <title>{name}</title>
  <desc>Horizontal lockup for light backgrounds. Icon plus name.</desc>
  <rect width="40" height="40" x="4" y="4" rx="9" fill="#1E3A5F"/>
  <path fill="none" stroke="#F8FAFC" stroke-width="1.6" stroke-linecap="square" d="M12 16h16M12 23h16M20 12v11"/>
  <rect x="21" y="24.2" width="6" height="6" rx="1.2" fill="#059669"/>
  <text x="56" y="22" fill="#0F172A" font-family="Microsoft JhengHei, PingFang TC, Noto Sans TC, sans-serif" font-size="20" font-weight="700">{name}</text>
  <text x="56" y="38" fill="#475569" font-family="Microsoft JhengHei, PingFang TC, Noto Sans TC, sans-serif" font-size="11">{tagline}</text>
</svg>
""",
    "lockup-inverse.svg": f"""<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 248 48" role="img">
  <title>{name}</title>
  <desc>Horizontal lockup for dark backgrounds.</desc>
  <rect x="4.75" y="4.75" width="38.5" height="38.5" rx="9" fill="none" stroke="#F8FAFC" stroke-width="1.75"/>
  <path fill="none" stroke="#F8FAFC" stroke-width="1.6" stroke-linecap="square" d="M12 16h16M12 23h16M20 12v11"/>
  <rect x="21" y="24.2" width="6" height="6" rx="1.2" fill="#059669"/>
  <text x="56" y="22" fill="#F8FAFC" font-family="Microsoft JhengHei, PingFang TC, Noto Sans TC, sans-serif" font-size="20" font-weight="700">{name}</text>
  <text x="56" y="38" fill="#CBD5E1" font-family="Microsoft JhengHei, PingFang TC, Noto Sans TC, sans-serif" font-size="11">{tagline}</text>
</svg>
""",
    "lockup-stacked.svg": f"""<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 128 148" role="img">
  <title>{name}</title>
  <desc>Stacked lockup for square avatars and app lists.</desc>
  <rect x="32" y="8" width="64" height="64" rx="14" fill="#1E3A5F"/>
  <path fill="none" stroke="#F8FAFC" stroke-width="2.4" stroke-linecap="square" d="M48 32h32M48 46h32M64 24v22"/>
  <rect x="66" y="48" width="12" height="12" rx="2.4" fill="#059669"/>
  <text x="64" y="104" text-anchor="middle" fill="#0F172A" font-family="Microsoft JhengHei, PingFang TC, Noto Sans TC, sans-serif" font-size="28" font-weight="700">{name}</text>
  <text x="64" y="128" text-anchor="middle" fill="#475569" font-family="Microsoft JhengHei, PingFang TC, Noto Sans TC, sans-serif" font-size="12">{tagline}</text>
</svg>
""",
}

for filename, content in files.items():
    path = root / filename
    path.write_text(content, encoding="utf-8", newline="\n")
    data = path.read_bytes()
    assert "\u6790\u5eab".encode("utf-8") in data, filename
    print(filename, len(data))
