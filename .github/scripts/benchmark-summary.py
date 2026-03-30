#!/usr/bin/env python3
"""Generate GitHub Step Summary from benchmarkResults.json."""

import json
import sys
import os


def fmt_ms(val):
    """Format milliseconds."""
    if val is None or val == 0:
        return "—"
    if val >= 1000:
        return f"{val:.0f} ms"
    return f"{val:.1f} ms"


def fmt_bytes(val):
    """Format bytes to human-readable."""
    if val is None or val == 0:
        return "0 B"
    if val < 0:
        return f"-{fmt_bytes(-val)}"
    if val < 1024:
        return f"{val} B"
    if val < 1024 * 1024:
        return f"{val / 1024:.1f} KB"
    return f"{val / (1024 * 1024):.1f} MB"


def ratio_str(a, b):
    """Calculate ratio b/a (higher = b is slower)."""
    if a is None or b is None or a == 0:
        return "—"
    r = b / a
    return f"{r:.1f}x"


def get_median(bench, test_name):
    """Get median from a benchmark test."""
    test = bench.get(test_name)
    if test is None:
        return None
    return test.get("median", 0)


def get_total(bench, test_name):
    """Get totalMs from a benchmark test."""
    test = bench.get(test_name)
    if test is None:
        return None
    return test.get("totalMs", 0)


def get_alloc(bench, test_name):
    """Get totalAlloc from a benchmark test."""
    test = bench.get(test_name)
    if test is None:
        return None
    return test.get("totalAlloc", 0)


def get_gc(bench, test_name):
    """Get GC counts from a benchmark test."""
    test = bench.get(test_name)
    if test is None:
        return "—"
    gc = test.get("gc", [0, 0, 0])
    if gc[0] == 0 and gc[1] == 0 and gc[2] == 0:
        return "0"
    return f"{gc[0]}/{gc[1]}/{gc[2]}"


def main():
    import argparse

    parser = argparse.ArgumentParser()
    parser.add_argument("json_path", help="Path to benchmarkResults.json")
    parser.add_argument("--commit", default="?", help="Git commit SHA")
    parser.add_argument("--branch", default="?", help="Git branch name")
    args = parser.parse_args()

    path = args.json_path

    if not os.path.exists(path):
        print("## Benchmark Results")
        print("")
        print("No benchmark results found.")
        sys.exit(0)

    with open(path, "r", encoding="utf-8") as f:
        data = json.load(f)

    commit_sha = args.commit[:8] if len(args.commit) > 8 else args.commit
    branch = args.branch

    si = data.get("systemInfo", {})
    cfg = data.get("config", {})
    text = data.get("textBenchmarks", {})
    glyph = data.get("glyphRasterization", {})
    errors = data.get("errors", [])

    # Header
    print("## Benchmark Results")
    print("")
    print(
        f"**Device:** {si.get('deviceModel', '?')} "
        f"| **OS:** {si.get('operatingSystem', '?')} "
        f"| **GPU:** {si.get('graphicsDeviceName', '?')}"
    )
    print(
        f"**CPU:** {si.get('processorType', '?')} ({si.get('processorCount', '?')} cores @ {si.get('processorFrequency', '?')} MHz) "
        f"| **RAM:** {si.get('systemMemorySize', '?')} MB"
    )
    print(
        f"**Unity:** {si.get('unityVersion', '?')} ({si.get('scriptingBackend', '?')}) "
        f"| **Screen:** {si.get('screenWidth', '?')}x{si.get('screenHeight', '?')} @ {si.get('screenDpi', '?')} dpi"
    )
    print(
        f"**Commit:** `{commit_sha}` "
        f"| **Branch:** `{branch}`"
    )
    print("")

    # Text Pipeline Table
    uni_st = text.get("unitextSingleThreaded", {})
    uni_par = text.get("unitextParallel", {})
    tmp = text.get("tmp", {})
    uitk = text.get("uiToolkit", {})

    tests = [
        ("Creation", "creation"),
        ("Destruction", "destruction"),
        ("Full Rebuild", "fullRebuild"),
        ("Layout (Wrap+NoAuto)", "layoutWrapNoAuto"),
        ("Layout (Wrap+Auto)", "layoutWrapAuto"),
        ("Layout (NoWrap+NoAuto)", "layoutNoWrapNoAuto"),
        ("Layout (NoWrap+Auto)", "layoutNoWrapAuto"),
        ("Mesh Rebuild", "meshRebuild"),
    ]

    obj_count = cfg.get("objectCount", "?")
    iters = cfg.get("iterations", "?")

    print(
        f"### Text Pipeline ({obj_count} objects x {iters} iterations)"
    )
    print("")
    print(
        "| Phase | UniText | Parallel | TMP | UIToolkit | vs TMP | vs UIToolkit |"
    )
    print("|-------|---------|----------|-----|-----------|--------|--------------|")

    for label, key in tests:
        u = get_total(uni_st, key)
        p = get_total(uni_par, key)
        t = get_total(tmp, key)
        ui = get_total(uitk, key)

        print(
            f"| {label} "
            f"| {fmt_ms(u)} "
            f"| {fmt_ms(p)} "
            f"| {fmt_ms(t)} "
            f"| {fmt_ms(ui)} "
            f"| {ratio_str(u, t)} "
            f"| {ratio_str(u, ui)} |"
        )

    print("")

    # Allocation table
    print("### Memory Allocation")
    print("")
    print("| Phase | UniText | TMP | UIToolkit |")
    print("|-------|---------|-----|-----------|")

    for label, key in tests:
        u = get_alloc(uni_st, key)
        t = get_alloc(tmp, key)
        ui = get_alloc(uitk, key)
        print(f"| {label} | {fmt_bytes(u)} | {fmt_bytes(t)} | {fmt_bytes(ui)} |")

    print("")

    # GC table
    print("### GC Collections (Gen0/Gen1/Gen2)")
    print("")
    print("| Phase | UniText | TMP | UIToolkit |")
    print("|-------|---------|-----|-----------|")

    for label, key in tests:
        u = get_gc(uni_st, key)
        t = get_gc(tmp, key)
        ui = get_gc(uitk, key)
        print(f"| {label} | {u} | {t} | {ui} |")

    print("")

    # Glyph Rasterization
    if glyph:
        print("### Glyph Rasterization")
        print("")

        uni_st_g = glyph.get("unitextSingleThreaded", {})
        uni_par_g = glyph.get("unitextParallel", {})
        tmp_g = glyph.get("tmp", {})

        glyphs = uni_st_g.get("uniqueGlyphs", "?")
        print(f"Unique glyphs: **{glyphs}**")
        print("")
        print("| Mode | Median | Per-glyph | Managed Alloc |")
        print("|------|--------|-----------|---------------|")

        for label, g in [
            ("UniText (ST)", uni_st_g),
            ("UniText (Parallel)", uni_par_g),
            ("TMP", tmp_g),
        ]:
            if not g:
                continue
            med = g.get("median", 0)
            ppg = g.get("perGlyphMedianUs", 0)
            alloc = g.get("managedAlloc", 0)
            print(
                f"| {label} "
                f"| {fmt_ms(med)} "
                f"| {ppg:.1f} us "
                f"| {fmt_bytes(alloc)} |"
            )

        print("")

    # Errors
    if errors:
        print("### Errors")
        print("")
        for err in errors:
            print(f"- {err}")
        print("")


if __name__ == "__main__":
    main()
