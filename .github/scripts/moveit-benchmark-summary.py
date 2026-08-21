#!/usr/bin/env python3
"""Render a GitHub Step Summary from a MoveIt benchmark result document.

Reads the `motionBenchmarks` section written by the shared benchmark runner and prints one table per
measured dimension: main-thread frame time per workload, and creation cost per engine. Every engine in
the run gets a column, so adding an adapter needs no change here.
"""

import argparse
import json
import sys


def series(node):
    """Median of a serialized sample series, or None when the series carries no samples."""
    if not isinstance(node, dict):
        return None
    value = node.get("median")
    return value if isinstance(value, (int, float)) else None


def fmt(value, unit=""):
    if value is None:
        return "n/a"
    if abs(value) >= 1000:
        return f"{value:,.0f}{unit}"
    return f"{value:.3f}{unit}"


def status_icon(status):
    return {"measured": "✅", "partial": "⚠️", "failed": "❌", "unsupported": "➖"}.get(status, "❔")


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("results", help="benchmarkResults.json")
    args = parser.parse_args()

    with open(args.results, encoding="utf-8") as f:
        data = json.load(f)

    motion = data.get("motionBenchmarks") or {}
    engines = motion.get("engines") or {}
    if not engines:
        print("## MoveIt Benchmark\n\nNo motion results in this run.")
        return 0

    meta = data.get("meta", {})
    info = data.get("systemInfo", {})
    names = list(engines.keys())

    print("## MoveIt Benchmark\n")
    print(f"`{meta.get('commit', 'unknown')[:12]}` on `{meta.get('branch', 'unknown')}` "
          f"({meta.get('source', 'unknown')}{', dirty' if meta.get('dirty') else ''})  ")
    print(f"{info.get('operatingSystem', 'unknown os')} · {info.get('processorType', 'unknown cpu')} · "
          f"{info.get('graphicsDeviceName', 'unknown gpu')}\n")

    print("### Engines\n")
    print("| Engine | Status | Version | Integration |")
    print("|---|---|---|---|")
    for name in names:
        engine = engines[name]
        md = engine.get("metadata", {})
        reason = engine.get("statusReason")
        status = f"{status_icon(engine.get('status'))} {engine.get('status', 'unknown')}"
        if reason:
            status += f" — {reason}"
        print(f"| {name} | {status} | {md.get('engineVersion', 'n/a')} | {md.get('integration', 'n/a')} |")

    workloads = []
    for name in names:
        for workload in (engines[name].get("workloads") or {}):
            if workload not in workloads:
                workloads.append(workload)

    if workloads:
        print("\n### Main-thread frame time, median ms\n")
        print("| Workload | " + " | ".join(names) + " |")
        print("|---" * (len(names) + 1) + "|")
        for workload in workloads:
            cells = []
            for name in names:
                node = (engines[name].get("workloads") or {}).get(workload) or {}
                if node.get("status") not in (None, "measured"):
                    cells.append(status_icon(node.get("status")))
                else:
                    cells.append(fmt(series(node.get("mainThread"))))
            print(f"| {workload} | " + " | ".join(cells) + " |")

    print("\n### Creation\n")
    print("| Pass | Metric | " + " | ".join(names) + " |")
    print("|---" * (len(names) + 2) + "|")
    for pass_name, label in (("firstBatch", "first batch"), ("warmRecycled", "warm recycled")):
        for key, metric, unit in (("timePerCreation", "time / motion", " µs"),
                                  ("gcBytesPerCreation", "GC bytes / motion", " B")):
            cells = []
            for name in names:
                node = ((engines[name].get("creation") or {}).get(pass_name) or {}).get(key)
                cells.append(fmt(series(node), unit))
            print(f"| {label} | {metric} | " + " | ".join(cells) + " |")

    errors = data.get("errors") or []
    if errors:
        print("\n### Errors\n")
        for error in errors:
            print(f"- {error}")

    return 0


if __name__ == "__main__":
    sys.exit(main())
