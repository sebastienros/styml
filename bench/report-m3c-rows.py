#!/usr/bin/env python3
# M3c gate 3: summarises an interleaved row file of
# bench/results/M3c-gate3 (lines '<round> <side> <row...> <value>', sides 'baseline', 'baseline2' and any others).
# Values are MB/s from bench/gate3-row.cs, or microseconds per call from 'gate3-row' in bench/Styml.Benchmarks (pass
# --time). Per row and per side other than the baselines: the median over rounds of the side's time ÷ the baseline's
# (the mean of the round's baseline runs when there are two), and the best of the side ÷ the best baseline; then the
# A/A median (second baseline ÷ first) when the file has it.
# Usage: python3 bench/report-m3c-rows.py <file> [--time]
import statistics
import sys


def main():
    path, is_time = sys.argv[1], "--time" in sys.argv
    runs = {}
    order = []
    sides = []
    with open(path, encoding="utf-8") as f:
        for line in f:
            parts = line.split()
            if not parts or parts[0].startswith("#") or len(parts) < 4:
                continue
            rnd, side, row, value = parts[0], parts[1], " ".join(parts[2:-1]), float(parts[-1])
            t = value if is_time else 1 / value
            if row not in runs:
                runs[row] = {}
                order.append(row)
            if side not in ("baseline", "baseline2") and side not in sides:
                sides.append(side)
            runs[row].setdefault(rnd, {})[side] = t
    header = "| Row | Rounds |"
    rule = "|---|---:|"
    for s in sides:
        header += f" {s} ÷ baseline, median | {s}, best ÷ best |"
        rule += "---:|---:|"
    print(header + " A/A (baseline 2 ÷ 1), median |")
    print(rule + "---:|")
    for row in order:
        aa, bases = [], []
        ratios = {s: [] for s in sides}
        bests = {s: [] for s in sides}
        rounds = 0
        for values in runs[row].values():
            b = [values[s] for s in ("baseline", "baseline2") if s in values]
            if not b:
                continue
            rounds += 1
            bases += b
            if len(b) == 2:
                aa.append(b[1] / b[0])
            for s in sides:
                if s in values:
                    ratios[s].append(values[s] / statistics.mean(b))
                    bests[s].append(values[s])
        text = f"| {row} | {rounds} |"
        for s in sides:
            if ratios[s]:
                text += f" {statistics.median(ratios[s]):.3f} | {min(bests[s]) / min(bases):.3f} |"
            else:
                text += " n/a | n/a |"
        aa_text = f"{statistics.median(aa):.3f}" if aa else "n/a"
        print(text + f" {aa_text} |")


if __name__ == "__main__":
    main()
