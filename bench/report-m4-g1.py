#!/usr/bin/env python3
# M4 gate G1: summarises an output of bench/run-m4-sides.sh. Per row and
# side: the median over rounds of the side's time / the mean of that round's two baseline runs (as
# bench/report-m3c-rows.py), and the A/A median (baseline 2 / baseline 1). Then per side: the geometric mean of the row
# medians, the largest row, and the rows above 1.03; and the same for the A/A. --rows prints the per-row table too,
# with the baseline's median time; --groups prints the geometric mean per row group (class and method).
# Usage: python3 bench/report-m4-g1.py <file> [--rows] [--groups]
import math
import statistics
import sys


def main():
    path = sys.argv[1]
    runs, order, sides = {}, [], []
    with open(path, encoding="utf-8") as f:
        for line in f:
            parts = line.split()
            if not parts or parts[0].startswith("#") or len(parts) < 4:
                continue
            rnd, side, row, value = parts[0], parts[1], " ".join(parts[2:-1]), float(parts[-1])
            if row not in runs:
                runs[row] = {}
                order.append(row)
            if side not in ("baseline", "baseline2") and side not in sides:
                sides.append(side)
            runs[row].setdefault(rnd, {})[side] = value
    medians = {s: {} for s in sides + ["A/A"]}
    base_time = {}
    for row in order:
        base_time[row] = statistics.median(v[s] for v in runs[row].values() for s in ("baseline", "baseline2") if s in v)
        ratios = {s: [] for s in sides + ["A/A"]}
        for values in runs[row].values():
            b = [values[s] for s in ("baseline", "baseline2") if s in values]
            if not b:
                continue
            if len(b) == 2:
                ratios["A/A"].append(b[1] / b[0])
            for s in sides:
                if s in values:
                    ratios[s].append(values[s] / statistics.mean(b))
        for s, r in ratios.items():
            if r:
                medians[s][row] = statistics.median(r)
    if "--rows" in sys.argv:
        print("| Row | Base, µs | " + " | ".join(f"{s} ÷ base" for s in sides) + " | A/A |")
        print("|---|---:|" + "---:|" * (len(sides) + 1))
        for row in order:
            cells = [f"{medians[s][row]:.3f}" if row in medians[s] else "n/a" for s in sides + ["A/A"]]
            print(f"| {row} | {base_time[row]:,.4f} | " + " | ".join(cells) + " |")
        print()
    if "--groups" in sys.argv:
        groups = list(dict.fromkeys(" ".join(r.split()[:2]) for r in order))
        print("| Row group | Rows | " + " | ".join(f"{s} ÷ base" for s in sides) + " | A/A |")
        print("|---|---:|" + "---:|" * (len(sides) + 1))
        for g in groups:
            rows = [r for r in order if " ".join(r.split()[:2]) == g]
            cells = []
            for s in sides + ["A/A"]:
                v = [medians[s][r] for r in rows if r in medians[s]]
                cells.append(f"{math.exp(statistics.mean(math.log(x) for x in v)):.4f}" if v else "n/a")
            print(f"| {g} | {len(rows)} | " + " | ".join(cells) + " |")
        print()
    print("| Side | Rows | Geometric mean | Largest row | Rows above 1.03 | Rows below 0.97 |")
    print("|---|---:|---:|---|---|---:|")
    for s in sides + ["A/A"]:
        m = medians[s]
        if not m:
            continue
        geo = math.exp(statistics.mean(math.log(v) for v in m.values()))
        worst = max(m, key=m.get)
        above = [f"{r} {v:.3f}" for r, v in m.items() if v > 1.03]
        below = sum(1 for v in m.values() if v < 0.97)
        label = "A/A (baseline 2 ÷ baseline 1)" if s == "A/A" else s
        print(f"| {label} | {len(m)} | {geo:.4f} | {worst} {m[worst]:.3f} | {'; '.join(above) if above else 'none'} | {below} |")


if __name__ == "__main__":
    main()
