#!/usr/bin/env python3
import sys
import os
import json
import glob

UNITEXT = {
    "unitextSingleThreaded",
    "unitextParallel",
    "unitextSingleThreadedMaxStroke",
    "unitextParallelMaxStroke",
}


def main():
    src = sys.argv[1] if len(sys.argv) > 1 else "."
    files = sorted(glob.glob(os.path.join(src, "benchmarkResults*.json")))
    if not files:
        print("No benchmarkResults*.json found — nothing to validate.")
        return 0

    unitext_fail = []
    baseline_fail = []
    unitext_total = 0
    for f in files:
        try:
            with open(f, encoding="utf-8") as fh:
                data = json.load(fh)
        except (json.JSONDecodeError, OSError) as e:
            print(f"  (skip unreadable {f}: {e})")
            continue
        glyph = data.get("glyphRasterization", {})
        dev = (data.get("systemInfo", {}) or {}).get("deviceModel") or os.path.basename(f)
        for ekey, byfont in glyph.items():
            if not isinstance(byfont, dict):
                continue
            for font, cell in byfont.items():
                if not isinstance(cell, dict):
                    continue
                is_unitext = ekey in UNITEXT
                if is_unitext:
                    unitext_total += 1
                st = cell.get("status", "measured")
                if st == "measured":
                    continue
                line = f"{dev} · {ekey} · {font}: {st} — {cell.get('statusReason', '')}"
                (unitext_fail if is_unitext else baseline_fail).append(line)

    if baseline_fail:
        print(f"Baseline (TMP/UIToolkit) not-measured on {len(baseline_fail)} cells (non-fatal):")
        for x in baseline_fail:
            print(f"  - {x}")

    if unitext_fail:
        print(f"::error::UniText glyph delivery FAILED on {len(unitext_fail)} cell(s):")
        for x in unitext_fail:
            print(f"  ::error::  {x}")
        return 1

    print(f"UniText glyph delivery OK: all {unitext_total} UniText cell(s) measured across {len(files)} device file(s).")
    return 0


if __name__ == "__main__":
    sys.exit(main())
