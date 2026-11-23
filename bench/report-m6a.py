#!/usr/bin/env python3
# Turns the BenchmarkDotNet CSV exports of M6a (bench/results/M6a/<tfm>/results/*-report.csv) into the Markdown tables:
# per scenario, MB/s with the time ratio against Styml (default options, YAML form) and against
# System.Text.Json, then allocated bytes. Usage, from the repository root: python3 bench/report-m6a.py net11.0 [net10.0]
import csv
import glob
import os
import sys

ORDER = ["MinifiedJson", "Tokenize", "TokenizeText", "DeserializeReflection", "DeserializeSourceGen", "FromString"]
TITLES = {
    "MinifiedJson": "Minified JSON (records-l.min.json, the shape of gate 5)",
    "Tokenize": "Tokenize",
    "TokenizeText": "Tokenize + text",
    "DeserializeReflection": "Deserialize, reflection",
    "DeserializeSourceGen": "Deserialize, source-generated",
    "FromString": "Deserialize from `string`, source-generated",
}


def number(text):
    text = text.strip().replace(",", "")
    for unit in (" ns", " B", " KB", " MB"):
        if text.endswith(unit):
            text = text[: -len(unit)]
    try:
        return float(text)
    except ValueError:
        return None


def load(tfm):
    data = {}
    for path in glob.glob(os.path.join("bench", "results", "M6a", tfm, "results", "*-report.csv")):
        cls = os.path.basename(path).split("-report")[0].split(".")[-1]
        with open(path, newline="", encoding="utf-8") as f:
            for row in csv.DictReader(f):
                data.setdefault(cls, []).append(row)
    return data


def mb(row):
    v = number(row.get("MB/s", ""))
    return "NA" if v is None else f"{v:,.0f}" if v >= 100 else f"{v:,.1f}"


def alloc(row):
    v = number(row.get("Allocated", ""))
    if v is None:
        return "NA" if row.get("Allocated", "").strip() not in ("-", "0 B") else "0"
    if v >= 1e6:
        return f"{v / 1e6:,.1f} MB"
    if v >= 1e3:
        return f"{v / 1e3:,.1f} KB"
    return f"{v:,.0f} B"


def ratio(a, b):
    if a is None or b is None or b == 0:
        return "NA"
    return f"{a / b:.2f}"


def table(cls, rows):
    methods = []
    for r in rows:
        if r["Method"] not in methods:
            methods.append(r["Method"])
    order = ["workflow-s", "compose-s", "k8s-s", "records-s", "workflow-m", "compose-m", "k8s-m", "openapi-m",
             "records-m", "openapi-l", "k8s-l", "records-l"]
    docs = sorted({r["Document"] for r in rows}, key=lambda d: order.index(d) if d in order else len(order))
    cell = {(r["Document"], r["Method"]): r for r in rows}

    # Minified JSON mixes tokenizing and deserializing: each method is compared with the Styml and System.Text.Json
    # methods of its own kind (same prefix).
    def prefix(m):
        return m.split("_")[0] + "_" if m.startswith(("Tokenize_", "Deserialize_")) else ""

    def base_of(m):
        return prefix(m) + ("Styml" if prefix(m) else "Styml_Yaml")

    def ref_of(m):
        return prefix(m) + "Stj"

    out = []
    against = "default options, on the same minified JSON" if cls == "MinifiedJson" else "default options, YAML form"
    out.append(f"MB/s (higher is better), and in brackets the time ratio against Styml ({against}) / against System.Text.Json (lower is better):")
    out.append("")
    out.append("| Document | " + " | ".join(f"`{m}`" for m in methods) + " |")
    out.append("|---|" + "---:|" * len(methods))
    def mean_of(d, m):
        return number(cell[(d, m)]["Mean"]) if (d, m) in cell else None

    for d in docs:
        cells = []
        for m in methods:
            r = cell.get((d, m))
            if r is None:
                cells.append("")
                continue
            mean = number(r["Mean"])
            base, ref = mean_of(d, base_of(m)), mean_of(d, ref_of(m))
            cells.append(f"{mb(r)} ({ratio(mean, base)} / {ratio(mean, ref)})")
        out.append(f"| {d} | " + " | ".join(cells) + " |")
    out.append("")
    out.append("Allocated per operation:")
    out.append("")
    out.append("| Document | " + " | ".join(f"`{m}`" for m in methods) + " |")
    out.append("|---|" + "---:|" * len(methods))
    for d in docs:
        out.append(f"| {d} | " + " | ".join(alloc(cell[(d, m)]) if (d, m) in cell else "" for m in methods) + " |")
    out.append("")
    return out


def main():
    for tfm in sys.argv[1:] or ["net11.0"]:
        data = load(tfm)
        for cls in ORDER:
            if cls not in data:
                print(f"### {TITLES[cls]}\n\n(no results)\n")
                continue
            print(f"### {TITLES[cls]}")
            print()
            print("\n".join(table(cls, data[cls])))


if __name__ == "__main__":
    main()
