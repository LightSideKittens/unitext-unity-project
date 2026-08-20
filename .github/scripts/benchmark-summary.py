#!/usr/bin/env python3
"""Generate GitHub Step Summary from benchmarkResults.json, and emit the viewer's per-suite site streams."""

import json
import sys
import os
import re
import datetime


def fmt_ms(val):
    """Format milliseconds."""
    if val is None or val == 0:
        return "—"
    if val >= 1000:
        return f"{val:.0f} ms"
    return f"{val:.1f} ms"


def fmt_bytes(val):
    """Format bytes to human-readable."""
    if val is None:
        return "—"
    if val == 0:
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


def get_managed_alloc(bench, test_name):
    """Get managed allocation traffic from a benchmark test."""
    test = bench.get(test_name)
    if test is None:
        return None
    return test.get("managedAlloc", 0)


def emit_streams(data, commit, branch, dirpath):
    """Write the viewer's per-suite site streams (run-text/glyph/motion-*.js) — the Python mirror of
    the runtime BenchmarkStreams.Split, so a CI run's combined JSON becomes drop-in files for Benchmarks/runs.
    Backfills the real commit/branch (which the on-device build could not read) when the JSON lacks them."""
    os.makedirs(dirpath, exist_ok=True)
    meta = data.setdefault("meta", {})
    if commit and commit not in ("?", "") and meta.get("commit") in (None, "", "unknown"):
        meta["commit"] = commit
    if branch and branch not in ("?", "") and meta.get("branch") in (None, "", "unknown"):
        meta["branch"] = branch

    si = data.get("systemInfo", {})
    plat = si.get("platform", "Unknown")
    dev = re.sub(r"[^A-Za-z0-9_-]", "-", si.get("deviceName") or "unknown")
    stamp = datetime.datetime.now(datetime.timezone.utc).strftime("%Y%m%d-%H%M%S") + f"-{plat}-{dev}"

    sections = ("textBenchmarks", "glyphRasterization", "motionBenchmarks")
    suites = [
        ("text", "textBenchmarks", "__unitextTextRuns"),
        ("glyph", "glyphRasterization", "__unitextGlyphRuns"),
        ("motion", "motionBenchmarks", "__unitextMotionRuns"),
    ]
    for suite, keep, g in suites:
        section = data.get(keep)
        if not isinstance(section, dict) or len(section) == 0:
            continue
        clone = dict(data)
        for section_name in sections:
            if section_name != keep:
                clone.pop(section_name, None)
        clone["suite"] = suite
        body = json.dumps(clone, indent=2)
        content = f"window.{g} = window.{g} || [];\nwindow.{g}.push(\n{body}\n);\n"
        out = os.path.join(dirpath, f"run-{suite}-{stamp}.js")
        with open(out, "w", encoding="utf-8") as f:
            f.write(content)
        print(f"Emitted {out}", file=sys.stderr)


def main():
    import argparse

    parser = argparse.ArgumentParser()
    parser.add_argument("json_path", help="Path to benchmarkResults.json")
    parser.add_argument("--commit", default="?", help="Git commit SHA")
    parser.add_argument("--branch", default="?", help="Git branch name")
    parser.add_argument("--streams-dir", default=None, help="If set, emit run-{suite}-*.js here and skip the summary")
    args = parser.parse_args()

    path = args.json_path

    if not os.path.exists(path):
        print("## Benchmark Results")
        print("")
        print("No benchmark results found.")
        sys.exit(0)

    with open(path, "r", encoding="utf-8") as f:
        data = json.load(f)

    if args.streams_dir:
        emit_streams(data, args.commit, args.branch, args.streams_dir)
        return

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
    print("### Managed Allocation Traffic")
    print("")
    print("| Phase | UniText | TMP | UIToolkit |")
    print("|-------|---------|-----|-----------|")

    for label, key in tests:
        u = get_managed_alloc(uni_st, key)
        t = get_managed_alloc(tmp, key)
        ui = get_managed_alloc(uitk, key)
        print(f"| {label} | {fmt_bytes(u)} | {fmt_bytes(t)} | {fmt_bytes(ui)} |")

    print("")

    # Glyph Rasterization (nested: engine -> font -> data; tolerates the old flat shape too)
    if glyph:
        print("### Glyph Rasterization")
        print("")
        print("| Engine · Font | Status | Glyphs | CPU Median | E2E Median | Per-glyph (E2E) | Managed Alloc |")
        print("|---|---|---|---|---|---|---|")

        labels = {
            "unitextSingleThreaded": "UniText ST",
            "unitextParallel": "UniText MT",
            "unitextSingleThreadedMaxStroke": "UniText ST +stroke",
            "unitextParallelMaxStroke": "UniText MT +stroke",
            "tmp": "TMP",
            "uiToolkit": "UI Toolkit",
        }
        for ekey, entry in glyph.items():
            label = labels.get(ekey, ekey)
            is_flat = isinstance(entry, dict) and ("median" in entry or "frameTimes" in entry)
            rows = [("—", entry)] if is_flat else entry.items()
            for font, g in rows:
                if not g:
                    continue
                status = g.get("status", "measured")
                status_cell = status if status == "measured" else f"**{status}**"
                e2e = g.get("e2eMedian")
                e2e_pg = g.get("perGlyphE2eMedianUs")
                print(
                    f"| {label} · {font} "
                    f"| {status_cell} "
                    f"| {g.get('uniqueGlyphs', '?')} "
                    f"| {fmt_ms(g.get('median', 0))} "
                    f"| {fmt_ms(e2e)} "
                    f"| {(f'{e2e_pg:.1f} us' if e2e_pg else '—')} "
                    f"| {fmt_bytes(g.get('managedAlloc', 0))} |"
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
