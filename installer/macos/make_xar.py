#!/usr/bin/env python3
"""把目錄做成未壓縮的 xar（macOS flat package 用）。"""
import os
import struct
import sys
import zlib
from xml.sax.saxutils import escape


def collect(abs_path, name, next_id):
    mode = os.stat(abs_path).st_mode & 0o777
    if os.path.isdir(abs_path) and not os.path.islink(abs_path):
        node = {
            "id": next_id[0],
            "name": name,
            "type": "directory",
            "mode": mode or 0o755,
            "children": [],
        }
        next_id[0] += 1
        for entry in sorted(os.listdir(abs_path)):
            node["children"].append(collect(os.path.join(abs_path, entry), entry, next_id))
        return node
    node = {
        "id": next_id[0],
        "name": name,
        "type": "file",
        "mode": mode or 0o644,
        "path": abs_path,
        "size": os.path.getsize(abs_path),
    }
    next_id[0] += 1
    return node


def assign_offsets(node, cursor):
    if node["type"] == "file":
        node["offset"] = cursor[0]
        cursor[0] += node["size"]
        return
    for child in node["children"]:
        assign_offsets(child, cursor)


def render(node):
    mode = f"{node['mode']:04o}"
    parts = [
        f'<file id="{node["id"]}">',
        f"<name>{escape(node['name'])}</name>",
        f"<type>{node['type']}</type>",
        f"<mode>{mode}</mode>",
        "<uid>0</uid>",
        "<gid>0</gid>",
    ]
    if node["type"] == "file":
        parts.append(
            "<data>"
            f"<offset>{node['offset']}</offset>"
            f"<length>{node['size']}</length>"
            f"<size>{node['size']}</size>"
            '<encoding style="application/octet-stream"/>'
            '<archived-checksum style="none"/>'
            '<extracted-checksum style="none"/>'
            "</data>"
        )
    else:
        for child in node["children"]:
            parts.append(render(child))
    parts.append("</file>")
    return "".join(parts)


def write_heap(handle, node):
    if node["type"] == "file":
        with open(node["path"], "rb") as source:
            while True:
                chunk = source.read(1024 * 1024)
                if not chunk:
                    break
                handle.write(chunk)
        return
    for child in node["children"]:
        write_heap(handle, child)


def main():
    src, dest = sys.argv[1], sys.argv[2]
    next_id = [1]
    children = []
    for entry in sorted(os.listdir(src)):
        children.append(collect(os.path.join(src, entry), entry, next_id))
    root = {"type": "directory", "children": children}
    assign_offsets(root, [0])
    body = "".join(render(child) for child in children)
    toc = (
        '<?xml version="1.0" encoding="UTF-8"?>'
        "<xar><toc><checksum style=\"none\"/>"
        f"{body}</toc></xar>"
    ).encode("utf-8")
    compressed = zlib.compress(toc)
    header = struct.pack(">IHHQQI", 0x78617221, 28, 1, len(compressed), len(toc), 0)
    with open(dest, "wb") as handle:
        handle.write(header)
        handle.write(compressed)
        write_heap(handle, root)
    print(dest)


if __name__ == "__main__":
    main()
