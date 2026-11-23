#!/usr/bin/env python3
# M4p, condition 6: the Allocated column (bytes per operation) of BenchmarkDotNet's CSV reports, one line per class,
# method and document. With two folders, the two values side by side and whether the second is higher.
# Usage: python3 bench/report-alloc.py <results folder> [<results folder after>]
import csv, glob, os, sys

def read(folder):
    values = {}
    for path in sorted(glob.glob(os.path.join(folder, "*-report.csv"))):
        cls = os.path.basename(path).split("-report")[0].split(".")[-1]
        with open(path, encoding="utf-8") as f:
            for row in csv.DictReader(f):
                values[(cls, row["Method"], row["Document"])] = row.get("Allocated", "NA")
    return values

before = read(sys.argv[1])
after = read(sys.argv[2]) if len(sys.argv) > 2 else None
print("| Class | Method | Document | Allocated, B" + (" before | after | change |" if after else " |"))
print("|---|---|---|---:" + ("|---:|---|" if after else "|"))
rose = 0
for key in sorted(before):
    if after is None:
        print(f"| {key[0]} | {key[1]} | {key[2]} | {before[key]} |")
        continue
    b, a = before[key], after.get(key, "missing")
    change = "same" if a == b else ("higher" if a.replace(" B", "").replace(",", "").isdigit() and b.replace(" B", "").replace(",", "").isdigit() and int(a.replace(" B", "").replace(",", "")) > int(b.replace(" B", "").replace(",", "")) else "differs")
    rose += change == "higher"
    print(f"| {key[0]} | {key[1]} | {key[2]} | {b} | {a} | {change} |")
if after is not None:
    print(f"\n{rose} rows higher after.")
