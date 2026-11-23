#!/usr/bin/env python3
# The README's benchmark tables from bench/run-panel.sh's results (BenchmarkDotNet CSV reports).
#   python3 bench/report-panel.py <results folder> full      the per-document tables of docs/benchmarks/README.md
#   python3 bench/report-panel.py <results folder> compact   the README's summary: each library's time against Styml
#                                                             (geometric mean over the documents), and its mean and
#                                                             allocation on k8s-m
#   python3 bench/report-panel.py <results folder> pivot     the README's pivot tables: libraries as columns, mean time and
#                                                             allocation on k8s-m, and the time multiples over the documents
#   python3 bench/report-panel.py <results folder> pivot <file>...   rewrites the block between "<!-- panel:pivot -->" and
#                                                             "<!-- /panel:pivot -->" in each file (README.md, PACKAGE.md)
# <results folder> is bench/results/<set>/<tfm>/results.
import csv
import math
import os
import sys

folder, mode = sys.argv[1], sys.argv[2]
targets = sys.argv[3:]
SAMPLE = "k8s-m"  # the compact table's absolute columns

SCENARIOS = [
    ("DeserializeSourceGen", "Deserialize, source-generated", "Styml_Yaml"),
    ("DeserializeReflection", "Deserialize, reflection", "Styml_Yaml"),
    ("SerializeYamlSourceGen", "Serialize, source-generated", "Styml_Block"),
    ("DomParse", "DOM parse", "Styml_Yaml"),
    ("Tokenize", "Tokenize (reader only)", "Styml_Yaml"),
]

LABELS = {
    "Styml_Yaml": "Styml",
    "Styml_Block": "Styml",
    "Styml_Block_Writer": "Styml, reused writer and buffer",
    "Styml_Node": "Styml `YamlNode`",
    "YamlDotNet": "YamlDotNet",
    "SharpYaml": "SharpYaml",
    "VYaml": "VYaml",
    "Stj": "System.Text.Json (JSON input)",
    "Stj_Node": "System.Text.Json `JsonNode` (JSON input)",
}
DOM_LABELS = {"Styml_Yaml": "Styml `YamlDocument`", "Stj": "System.Text.Json `JsonDocument` (JSON input)"}
SERIALIZE_STJ = "System.Text.Json (JSON output)"


def label(cls, method):
    if cls == "DomParse" and method in DOM_LABELS:
        return DOM_LABELS[method]
    if cls == "SerializeYamlSourceGen" and method == "Stj":
        return SERIALIZE_STJ
    return LABELS.get(method, method)


def number(text):
    text = text.replace(",", "").split(" ")[0]
    return float(text) if text not in ("", "NA", "-") else None


def size(text):
    parts = text.replace(",", "").split(" ")
    if parts[0] in ("", "NA", "-"):
        return None
    value = float(parts[0])
    unit = parts[1] if len(parts) > 1 else "B"
    return value * {"B": 1, "KB": 1024, "MB": 1024 * 1024}[unit]


def load(cls):
    path = os.path.join(folder, f"Styml.Benchmarks.{cls}-report.csv")
    rows = {}
    for row in csv.DictReader(open(path, encoding="utf-8")):
        rows[(row["Document"], row["Method"])] = (number(row["Mean"]), number(row["Error"]), size(row["Allocated"]))
    return rows


def fmt_time(ns):
    return f"{ns / 1000:,.1f}"


def fmt_bytes(b):
    if b is None:
        return "?"
    if b >= 1024 * 1024:
        return f"{b / 1024 / 1024:,.2f} MB"
    if b >= 1024:
        return f"{b / 1024:,.1f} KB"
    return f"{b:,.0f} B"


# The pivot tables: one row per scenario, one column per library, Styml's cell bold, "n/a" where a library has no
# such API (None) and "—" where the panel does not measure it (""). Each row: (title, class, methods by column).
PIVOT_COLUMNS = ["Styml", "VYaml", "SharpYaml", "YamlDotNet", "System.Text.Json"]
PIVOT_ROWS = [
    ("Deserialize, source-generated", "DeserializeSourceGen", ["Styml_Yaml", "VYaml", "SharpYaml", "YamlDotNet", "Stj"]),
    ("Deserialize, reflection", "DeserializeReflection", ["Styml_Yaml", None, "SharpYaml", "YamlDotNet", "Stj"]),
    ("Serialize, source-generated", "SerializeYamlSourceGen", ["Styml_Block", "VYaml", "SharpYaml", "YamlDotNet", "Stj"]),
    ("Serialize, reused buffer", "SerializeYamlSourceGen", ["Styml_Block_Writer", "VYaml", "SharpYaml", "", ""]),
    ("DOM parse (`YamlDocument`, `JsonDocument`)", "DomParse", ["Styml_Yaml", None, "SharpYaml", "YamlDotNet", "Stj"]),
    ("DOM parse, mutable (`YamlNode`, `JsonNode`)", "DomParse", ["Styml_Node", None, "SharpYaml", "YamlDotNet", "Stj_Node"]),
    ("Tokenize", "Tokenize", ["Styml_Yaml", "VYaml", "SharpYaml", "YamlDotNet", "Stj"]),
]


def pivot_tables():
    data = {cls: load(cls) for cls in {r[1] for r in PIVOT_ROWS}}
    documents = list(dict.fromkeys(d for d, _ in data["Tokenize"]))
    sample = "k8s-m"
    header = "| Scenario | " + " | ".join(PIVOT_COLUMNS) + " |\n| --- |" + " ---: |" * len(PIVOT_COLUMNS)

    def table(cell):
        lines = [header]
        for title, cls, methods in PIVOT_ROWS:
            cells = []
            for i, m in enumerate(methods):
                text = "n/a" if m is None else "—" if m == "" else cell(data[cls], m)
                cells.append(f"**{text}**" if i == 0 and m is not None else text)
            lines.append(f"| {title} | " + " | ".join(cells) + " |")
        return "\n".join(lines)

    def mean(rows, m):
        return fmt_time(rows[(sample, m)][0])

    def allocated(rows, m):
        return fmt_bytes(rows[(sample, m)][2])

    def multiple_cell(cls, base):
        def cell(rows, m):
            ratios = [rows[(d, m)][0] / rows[(d, base)][0] for d in documents if (d, m) in rows]
            return f"{math.exp(sum(math.log(x) for x in ratios) / len(ratios)):.2f}×"
        return cell

    lines = []
    for title, cls, methods in PIVOT_ROWS:
        cells = []
        for i, m in enumerate(methods):
            text = "n/a" if m is None else "—" if m == "" else multiple_cell(cls, methods[0])(data[cls], m)
            cells.append(f"**{text}**" if i == 0 and m is not None else text)
        lines.append(f"| {title} | " + " | ".join(cells) + " |")
    multiples = header + "\n" + "\n".join(lines)
    return (f"Mean time on {sample} (microseconds; smaller is better):\n\n" + table(mean)
            + f"\n\nAllocated on {sample} (smaller is better):\n\n" + table(allocated)
            + "\n\nTime against Styml's, the geometric mean over the five documents (below 1 is faster than Styml):\n\n"
            + multiples)


if mode == "pivot":
    text = pivot_tables()
    if not targets:
        print(text)
    for target in targets:
        content = open(target, encoding="utf-8").read()
        start, end = "<!-- panel:pivot -->", "<!-- /panel:pivot -->"
        a, b = content.index(start) + len(start), content.index(end)
        open(target, "w", encoding="utf-8").write(content[:a] + "\n" + text + "\n" + content[b:])
        print("updated", target)
    sys.exit(0)

out = []
for cls, title, base in SCENARIOS:
    rows = load(cls)
    documents = list(dict.fromkeys(d for d, _ in rows))
    methods = list(dict.fromkeys(m for _, m in rows))
    if mode == "full":
        out.append(f"### {title}\n")
        out.append("| Document | Library | Mean (µs) | Error (µs) | Time vs Styml | Allocated | Allocated vs Styml |")
        out.append("| --- | --- | ---: | ---: | ---: | ---: | ---: |")
        for d in documents:
            b_mean, _, b_alloc = rows[(d, base)]
            for m in methods:
                if (d, m) not in rows:
                    continue
                mean, err, alloc = rows[(d, m)]
                ratio = f"{mean / b_mean:.2f}" if mean else "?"
                aratio = f"{alloc / b_alloc:.2f}" if alloc is not None and b_alloc else "?"
                out.append(f"| {d} | {label(cls, m)} | {fmt_time(mean)} | {fmt_time(err)} | {ratio} | {fmt_bytes(alloc)} | {aratio} |")
        out.append("")
    else:
        for m in methods:
            times = [rows[(d, m)][0] / rows[(d, base)][0] for d in documents if (d, m) in rows]
            gt = math.exp(sum(math.log(t) for t in times) / len(times))
            mean, _, alloc = rows.get((SAMPLE, m), (None, None, None))
            out.append(f"| {title} | {label(cls, m)} | {gt:.2f}× | {fmt_time(mean) if mean else '?'} | {fmt_bytes(alloc)} |")
if mode == "compact":
    out.insert(0, f"| Scenario | Library | Time vs Styml (5 documents) | {SAMPLE} mean (µs) | {SAMPLE} allocated |\n| --- | --- | ---: | ---: | ---: |")
print("\n".join(out))
