#!/usr/bin/env python3
# M4b's reader step: compares bench/results/M4b/reader/deserialize/head-{1,2} with
# baseline-{1,2}, as bench/report-m3c-gate3.py does for M3c: per class the A/A figure (baseline 2 / baseline 1), and the
# head (mean of its two runs) against each baseline run (geometric mean of the time ratios), then every row above 1.03
# against both baseline runs, which goes to the interleaved re-run (bench/run-m3c-sides.sh).
# Usage, from the repository root: python3 bench/report-m4b-reader.py
import csv
import math
import os

ROOT = os.path.join("bench", "results", "M4b", "reader", "deserialize")
CLASSES = ["DeserializeSourceGen", "DeserializeReflection", "FromString", "MinifiedJson"]


def load(run, cls):
    path = os.path.join(ROOT, run, "net11.0", "results", f"Styml.Benchmarks.{cls}-report.csv")
    with open(path, newline="", encoding="utf-8") as f:
        return {(r["Method"], r["Document"]): float(r["Mean"].replace(",", "").replace(" ns", "")) for r in csv.DictReader(f)}


def geomean(values):
    return math.exp(sum(math.log(v) for v in values) / len(values))


def main():
    print("| Class | Rows | A/A geometric mean | A/A largest row | Head / baseline 1 | Head / baseline 2 | Rows above 1.03 against both |")
    print("|---|---:|---:|---:|---:|---:|---:|")
    flagged = []
    for cls in CLASSES:
        b1, b2, h1, h2 = (load(r, cls) for r in ("baseline-1", "baseline-2", "head-1", "head-2"))
        rows = sorted(set(b1) & set(b2) & set(h1) & set(h2))
        aa = [b2[r] / b1[r] for r in rows]
        head = {r: (h1[r] + h2[r]) / 2 for r in rows}
        r1 = [head[r] / b1[r] for r in rows]
        r2 = [head[r] / b2[r] for r in rows]
        over = [(r, head[r] / b1[r], head[r] / b2[r]) for r in rows if head[r] / b1[r] > 1.03 and head[r] / b2[r] > 1.03]
        flagged += [(cls,) + o for o in over]
        print(f"| {cls} | {len(rows)} | {geomean(aa):.3f} | {max(aa, key=lambda x: abs(math.log(x))):.3f} | {geomean(r1):.3f} | {geomean(r2):.3f} | {len(over)} |")
    print()
    for cls, (method, doc), x1, x2 in flagged:
        print(f"- {cls}:{method}:{doc}  {x1:.3f} {x2:.3f}")


if __name__ == "__main__":
    main()
