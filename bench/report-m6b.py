#!/usr/bin/env python3
# Turns the BenchmarkDotNet CSV exports of M6b (bench/results/M6b/<tfm>/results/*-report.csv) into the Markdown tables:
# per scenario and document, each row's mean, its time ratio against the class's Styml baseline
# and against System.Text.Json, and allocated bytes. 'seed' prints the list of the rows where Styml is not
# the fastest YAML library, or allocates more than another.
# 'noisy' counts, per class, the cells whose standard deviation is above 5% of their mean (marked † in the tables).
# Usage, from the repository root: python3 bench/report-m6b.py tables <tfm> [class ...] | seed <tfm> ... | noisy <tfm> ...
import csv
import glob
import os
import sys

ORDER = ["SerializeYamlReflection", "SerializeYamlSourceGen", "SerializeToString", "DomParse", "AnchorsTokenize",
         "AnchorsDomParse", "AnchorsDeserializeReflection", "AnchorsDeserializeSourceGen", "AnchorsSerialize",
         "Tokenize", "TokenizeText", "DeserializeReflection", "DeserializeSourceGen"]
DOCS = ["workflow-s", "compose-s", "k8s-s", "records-s", "workflow-m", "compose-m", "k8s-m", "openapi-m", "records-m",
        "openapi-l", "k8s-l", "records-l", "anchors-s", "anchors-m", "merge-s", "merge-m"]
OTHERS = ("YamlDotNet", "SharpYaml", "VYaml")

# The Styml row each other library is compared with (the like-for-like pair; otherwise the class's baseline).
PAIR = {
    ("SerializeYamlReflection", "SharpYaml"): "Styml_Block_Writer",
    ("SerializeYamlSourceGen", "SharpYaml"): "Styml_Block_Writer",
    ("SerializeYamlSourceGen", "VYaml"): "Styml_Block_Writer",
}


def number(text):
    text = (text or "").strip().replace(",", "")
    scale = 1.0
    for unit, s in ((" ns", 1.0), (" μs", 1e3), (" us", 1e3), (" ms", 1e6), (" B", 1.0), (" KB", 1024.0), (" MB", 1024.0 ** 2)):
        if text.endswith(unit):
            text, scale = text[: -len(unit)], s
            break
    try:
        return float(text) * scale
    except ValueError:
        return None


def load(tfm):
    data = {}
    for path in glob.glob(os.path.join("bench", "results", "M6b", tfm, "results", "*-report.csv")):
        cls = os.path.basename(path).split("-report")[0].split(".")[-1]
        with open(path, newline="", encoding="utf-8") as f:
            data[cls] = list(csv.DictReader(f))
    return data


def methods_of(rows):
    out = []
    for r in rows:
        if r["Method"] not in out:
            out.append(r["Method"])
    return out


def baseline_of(rows):
    for r in rows:
        if (r.get("Ratio") or "").strip() == "1.00" and r["Method"].startswith("Styml"):
            return r["Method"]
    return methods_of(rows)[0]


def time(ns):
    if ns is None:
        return "NA"
    if ns >= 1e6:
        return f"{ns / 1e6:,.2f} ms"
    if ns >= 1e3:
        return f"{ns / 1e3:,.1f} μs"
    return f"{ns:,.0f} ns"


def size(b):
    if b is None:
        return "NA"
    if b >= 1024 ** 2:
        return f"{b / 1024 ** 2:,.2f} MB"
    if b >= 1024:
        return f"{b / 1024:,.1f} KB"
    return f"{b:,.0f} B"


def ratio(a, b):
    return "NA" if a is None or not b else f"{a / b:.2f}"


def noisy(row):
    """A cell whose standard deviation over its iterations is above 5% of its mean."""
    mean, sd = number(row["Mean"]), number(row["StdDev"])
    return mean is not None and sd is not None and mean > 0 and sd / mean > 0.05


def table(cls, rows):
    methods = methods_of(rows)
    base = baseline_of(rows)
    stj = next((m for m in methods if m.startswith("Stj")), None)
    cell = {(r["Document"], r["Method"]): r for r in rows}
    docs = sorted({r["Document"] for r in rows}, key=lambda d: DOCS.index(d) if d in DOCS else len(DOCS))
    out = [f"Mean, and in brackets the time ratio against `{base}`" + (f" / against `{stj}`" if stj else "") + " (lower is better); † marks a"
           " standard deviation above 5% of the mean over the 3 iterations:", ""]
    out.append("| Document | " + " | ".join(f"`{m}`" for m in methods) + " |")
    out.append("|---|" + "---:|" * len(methods))
    for d in docs:
        b = number(cell[(d, base)]["Mean"]) if (d, base) in cell else None
        s = number(cell[(d, stj)]["Mean"]) if stj and (d, stj) in cell else None
        cells = []
        for m in methods:
            r = cell.get((d, m))
            if r is None:
                cells.append("n/a")
                continue
            mean = number(r["Mean"])
            mark = " †" if noisy(r) else ""
            cells.append(f"{time(mean)}{mark} ({ratio(mean, b)}" + (f" / {ratio(mean, s)})" if stj else ")"))
        out.append(f"| {d} | " + " | ".join(cells) + " |")
    out += ["", "Allocated per operation:", ""]
    out.append("| Document | " + " | ".join(f"`{m}`" for m in methods) + " |")
    out.append("|---|" + "---:|" * len(methods))
    for d in docs:
        out.append(f"| {d} | " + " | ".join(size(number(cell[(d, m)]["Allocated"])) if (d, m) in cell else "n/a" for m in methods) + " |")
    out.append("")
    return out


def seed(tfm, data):
    """Every (class, document, library) where a YAML library beats Styml's row on time or allocation."""
    out = []
    for cls in ORDER:
        rows = data.get(cls)
        if not rows:
            continue
        base = baseline_of(rows)
        cell = {(r["Document"], r["Method"]): r for r in rows}
        docs = sorted({r["Document"] for r in rows}, key=lambda d: DOCS.index(d) if d in DOCS else len(DOCS))
        for d in docs:
            for m in methods_of(rows):
                if not m.startswith(OTHERS) or (d, m) not in cell:
                    continue
                mine = PAIR.get((cls, m.split("_")[0]), base)
                if (d, mine) not in cell:
                    continue
                st, ot = number(cell[(d, mine)]["Mean"]), number(cell[(d, m)]["Mean"])
                sa, oa = number(cell[(d, mine)]["Allocated"]), number(cell[(d, m)]["Allocated"])
                faster = ot is not None and st is not None and ot < st
                leaner = oa is not None and sa is not None and oa < sa
                if faster or leaner:
                    what = []
                    if faster:
                        what.append(f"time {time(ot)} against {time(st)} ({st / ot:.2f}x)")
                    if leaner:
                        what.append(f"allocates {size(oa)} against {size(sa)}")
                    out.append(f"| {tfm} | {cls} | {d} | `{m}` vs `{mine}` | {'; '.join(what)} |")
    return out


def main():
    mode, tfm = sys.argv[1], sys.argv[2]
    data = load(tfm)
    if mode == "noisy":
        for t in sys.argv[2:]:
            d = load(t)
            for cls in ORDER:
                if cls in d:
                    marked = [r for r in d[cls] if noisy(r)]
                    print(f"| {t} | {cls} | {len(marked)} of {len(d[cls])} | " + ", ".join(f"{r['Document']} `{r['Method']}` {100 * number(r['StdDev']) / number(r['Mean']):.0f}%" for r in marked) + " |")
        return
    if mode == "tables":
        for cls in sys.argv[3:] or ORDER:
            if cls in data:
                print(f"#### {cls} ({tfm})\n")
                print("\n".join(table(cls, data[cls])))
    else:
        for t in sys.argv[2:]:
            print("\n".join(seed(t, load(t))))


if __name__ == "__main__":
    main()
