#!/usr/bin/env python3
import sys
import os
import re
import zipfile

ART_RE = re.compile(r"^BenchmarkResults-(?P<suite>[^-]+)-(?P<unity>[^-]+)-(?P<platform>.+)$")
SHOT_RE = re.compile(
    r"bench-(?P<ord>\d+)-glyph-UniText-(?P<mode>.+)-(?P<tag>warmup|iter-\d+)\.png$", re.IGNORECASE
)


def device_of(png_path):
    parent = os.path.basename(os.path.dirname(png_path))
    return "" if parent in ("screenshots", "") else parent


def main():
    src, dst = sys.argv[1], sys.argv[2]
    os.makedirs(dst, exist_ok=True)

    platforms = {}

    for entry in sorted(os.listdir(src)):
        full = os.path.join(src, entry)
        m = ART_RE.match(entry)
        if not os.path.isdir(full) or not m:
            continue
        unity, platform = m.group("unity"), m.group("platform")
        p = platforms.setdefault(platform, {"runs": [], "shots": {}})
        for root, _dirs, files in os.walk(full):
            for fn in files:
                fp = os.path.join(root, fn)
                if fn.startswith("run-") and fn.endswith(".js"):
                    p["runs"].append(fp)
                    continue
                sm = SHOT_RE.search(fn)
                if sm:
                    key = (unity, device_of(fp), sm.group("mode"))
                    ordv = int(sm.group("ord"))
                    cur = p["shots"].get(key)
                    if cur is None or ordv > cur[0]:
                        p["shots"][key] = (ordv, fp)

    if not platforms:
        print("No benchmark artifacts matched — nothing to assemble.")
        return

    for platform, data in sorted(platforms.items()):
        zpath = os.path.join(dst, f"{platform}.zip")
        with zipfile.ZipFile(zpath, "w", zipfile.ZIP_DEFLATED) as z:
            seen = set()
            for fp in sorted(data["runs"]):
                name = os.path.basename(fp)
                arc = f"runs/{name}"
                n = 1
                while arc in seen:
                    stem, ext = os.path.splitext(name)
                    arc = f"runs/{stem}-{n}{ext}"
                    n += 1
                seen.add(arc)
                z.write(fp, arc)
            for (unity, device, mode), (_ord, fp) in sorted(data["shots"].items()):
                tag = f"{unity}-{device}" if device else unity
                z.write(fp, f"screenshots/{tag}-UniText-{mode}-last.png")
        print(f"{platform}.zip: {len(data['runs'])} run files, {len(data['shots'])} UniText screenshots")


if __name__ == "__main__":
    main()
