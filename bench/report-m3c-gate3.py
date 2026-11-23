#!/usr/bin/env python3
# M3c gate 3: compares bench/results/M3c-gate3/head-{1,2} with
# baseline-{1,2}. For each class: the A/A figure (baseline-2 / baseline-1), and the head (mean of its two runs)
# against each baseline run: geometric mean of the time ratios, and the rows above 1.03. Usage, from the repository
# root: python3 bench/report-m3c-gate3.py. For reference, the head is also compared with the
# committed bench/results/M6a/net11.0 and with the baseline taken at the spike, bench/results/M3c-baseline/net11.0.
import csv
import math
import os

ROOT = os.path.join("bench", "results", "M3c-gate3")
CLASSES = ["Tokenize", "MinifiedJson", "DeserializeSourceGen"]


def load(run, cls, root=ROOT):
    path = os.path.join(root, run, "net11.0", "results", f"Styml.Benchmarks.{cls}-report.csv")
    with open(path, newline="", encoding="utf-8") as f:
        return {(r["Method"], r["Document"]): float(r["Mean"].replace(",", "").replace(" ns", "")) for r in csv.DictReader(f)}


def geomean(values):
    return math.exp(sum(math.log(v) for v in values) / len(values))


def main():
    print("| Class | Rows | A/A geometric mean (baseline 2 / 1) | A/A largest row | Head / baseline 1 | Head / baseline 2 | Rows above 1.03 against both |")
    print("|---|---:|---:|---:|---:|---:|---:|")
    flagged_all = []
    for cls in CLASSES:
        b1, b2, h1, h2 = (load(r, cls) for r in ("baseline-1", "baseline-2", "head-1", "head-2"))
        keys = sorted(set(b1) & set(b2) & set(h1) & set(h2))
        head = {k: (h1[k] + h2[k]) / 2 for k in keys}
        aa = [b2[k] / b1[k] for k in keys]
        r1 = [head[k] / b1[k] for k in keys]
        r2 = [head[k] / b2[k] for k in keys]
        aa_max = max(aa, key=lambda v: abs(math.log(v)))
        flagged = [(k, head[k] / b1[k], head[k] / b2[k]) for k in keys if head[k] / b1[k] > 1.03 and head[k] / b2[k] > 1.03]
        flagged_all += [(cls,) + f for f in flagged]
        print(f"| {cls} | {len(keys)} | {geomean(aa):.3f} | {aa_max:.3f} | {geomean(r1):.3f} | {geomean(r2):.3f} | {len(flagged)} |")
    print()
    if flagged_all:
        print("Rows above 1.03 against both baseline runs:")
        print()
        print("| Class | Method | Document | Head / baseline 1 | Head / baseline 2 |")
        print("|---|---|---|---:|---:|")
        for cls, (method, doc), x1, x2 in flagged_all:
            print(f"| {cls} | {method} | {doc} | {x1:.3f} | {x2:.3f} |")
    print()
    print("For reference, the head against earlier runs (geometric mean of the time ratios):")
    print()
    print("| Class | Head / M6a (bench/results/M6a) | Head / spike baseline (bench/results/M3c-baseline) | Sitting baseline / spike baseline |")
    print("|---|---:|---:|---:|")
    results = os.path.join("bench", "results")
    for cls in CLASSES:
        b1, b2, h1, h2 = (load(r, cls) for r in ("baseline-1", "baseline-2", "head-1", "head-2"))
        m6a, spike = load("M6a", cls, results), load("M3c-baseline", cls, results)
        keys = sorted(set(b1) & set(b2) & set(h1) & set(h2) & set(m6a) & set(spike))
        head = {k: (h1[k] + h2[k]) / 2 for k in keys}
        base = {k: (b1[k] + b2[k]) / 2 for k in keys}
        print(f"| {cls} | {geomean([head[k] / m6a[k] for k in keys]):.3f} | {geomean([head[k] / spike[k] for k in keys]):.3f} | {geomean([base[k] / spike[k] for k in keys]):.3f} |")


if __name__ == "__main__":
    main()
