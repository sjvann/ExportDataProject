import re
import urllib.request

html = urllib.request.urlopen("http://localhost:5107/").read().decode("utf-8")
srcs = re.findall(r'src="([^"]+)"', html)
print("srcs", srcs[:4])
url = "http://localhost:5107" + srcs[0]
with urllib.request.urlopen(url) as response:
    data = response.read()
    print("status", response.status)
    print("type", response.headers.get("Content-Type"))
    print("len", len(data))
    print(data[:200])
