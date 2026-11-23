#!/usr/bin/env python3
# M4 gate G3 from M4a: summarises an output of bench/run-m4a-g3.sh. Per document, the median over rounds of each row,
# then the ratios of G3: block Write ÷ Utf8JsonWriter indented (target at most 1.3), and block Serialize ÷
# System.Text.Json (target at most 1.5 as a geometric mean, no document above 2.0), reflection and source-generated.
# The Write increment (block - JSON indented) is split between keys and values with the constant-key and
# constant-value streams: keys = increment - (Block_ConstKeys - Stj_Indented_ConstKeys), and likewise for values.
# From M4b, the non-ASCII rows (WriteBlockNonAscii): block ÷ the writer's JSON-compatible indented style with the relaxed
# encoder, per string set and input form (target at most 1.5; the ascii set is the control), with Utf8JsonWriter's row.
# Usage: python3 bench/report-m4a-g3.py <file>
import math
import statistics
import sys
from collections import defaultdict

ORDER = ["workflow-s", "compose-s", "k8s-s", "records-s", "workflow-m", "compose-m", "k8s-m", "openapi-m", "records-m", "openapi-l", "k8s-l", "records-l"]


def main():
    runs = defaultdict(list)
    with open(sys.argv[1], encoding="utf-8") as f:
        for line in f:
            parts = line.split()
            if len(parts) != 5 or parts[0].startswith("#"):
                continue
            _, cls, method, doc, value = parts
            runs[(cls, method, doc)].append(float(value))
    m = {k: statistics.median(v) for k, v in runs.items()}
    docs = [d for d in ORDER if ("WriteBlock", "Styml_Block", d) in m]
    non_ascii(m)
    if not docs:
        return

    def g(values):
        return math.exp(statistics.mean(math.log(v) for v in values))

    print("| Document | Write block, µs | Utf8JsonWriter indented, µs | Write ÷ | Increment, µs | Keys | Values | Other | Reflection block ÷ STJ | Source-gen block ÷ STJ |")
    print("|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|")
    w, r, s = [], [], []
    for d in docs:
        b = m[("WriteBlock", "Styml_Block", d)]
        j = m[("WriteBlock", "Stj_Indented", d)]
        inc = b - j
        keys = inc - (m[("WriteBlock", "Styml_Block_ConstKeys", d)] - m[("WriteBlock", "Stj_Indented_ConstKeys", d)])
        values = inc - (m[("WriteBlock", "Styml_Block_ConstValues", d)] - m[("WriteBlock", "Stj_Indented_ConstValues", d)])
        rr = m[("SerializeReflectionBlock", "Styml_Block", d)] / m[("SerializeReflectionBlock", "Stj", d)]
        ss = m[("SerializeSourceGenBlock", "Styml_Block", d)] / m[("SerializeSourceGenBlock", "Stj", d)]
        w.append(b / j); r.append(rr); s.append(ss)
        share = lambda x: f"{100 * x / inc:.0f}%" if inc > 0 else "n/a"
        print(f"| {d} | {b:,.3f} | {j:,.3f} | {b / j:.2f} | {inc:,.3f} | {share(keys)} | {share(values)} | {share(inc - keys - values)} | {rr:.2f} | {ss:.2f} |")
    print(f"| **Geometric mean** | | | **{g(w):.2f}** | | | | | **{g(r):.2f}** | **{g(s):.2f}** |")
    print()
    print(f"Write: largest {max(w):.2f} ({docs[w.index(max(w))]}); reflection: largest {max(r):.2f} ({docs[r.index(max(r))]}); source-generated: largest {max(s):.2f} ({docs[s.index(max(s))]}).")

    # The YAML libraries, over Styml's block output (G3: Styml faster than each, on every document).
    libraries = [("SerializeReflection", "YamlDotNet", "SerializeReflectionBlock"), ("SerializeReflection", "SharpYaml", "SerializeReflectionBlock"),
                 ("SerializeSourceGen", "YamlDotNet", "SerializeSourceGenBlock"), ("SerializeSourceGen", "SharpYaml", "SerializeSourceGenBlock"),
                 ("SerializeSourceGen", "VYaml", "SerializeSourceGenBlock")]
    if any((c, l, docs[0]) in m for c, l, _ in libraries):
        print()
        print("| Document | " + " | ".join(f"{l} {'reflection' if c == 'SerializeReflection' else 'source-gen'} ÷ Styml block" for c, l, _ in libraries) + " |")
        print("|---|" + "---:|" * len(libraries))
        lowest = {}
        for d in docs:
            cells = []
            for c, l, b in libraries:
                if (c, l, d) in m:
                    x = m[(c, l, d)] / m[(b, "Styml_Block", d)]
                    lowest[(c, l)] = min(lowest.get((c, l), x), x)
                    cells.append(f"{x:.2f}")
                else:
                    cells.append("n/a")
            print(f"| {d} | " + " | ".join(cells) + " |")
        print()
        print("Lowest: " + "; ".join(f"{l} {c}: {v:.2f}" for (c, l), v in lowest.items()) + ".")


def non_ascii(m):
    sets = [d for d in ("ascii", "latin", "cjk") if ("WriteBlockNonAscii", "Styml_Block", d) in m]
    if not sets:
        return
    print("| String set | Input | Block, µs | JSON-compatible indented, relaxed, µs | Utf8JsonWriter indented, relaxed, µs | Block ÷ JSON-compatible |")
    print("|---|---|---:|---:|---:|---:|")
    for d in sets:
        for suffix, form in (("", "string"), ("_Utf8", "UTF-8")):
            b = m[("WriteBlockNonAscii", "Styml_Block" + suffix, d)]
            j = m[("WriteBlockNonAscii", "Styml_Indented_Relaxed" + suffix, d)]
            t = m.get(("WriteBlockNonAscii", "Stj_Indented_Relaxed" + suffix, d))
            print(f"| {d} | {form} | {b:,.3f} | {j:,.3f} | {t:,.3f} | {b / j:.2f} |" if t else f"| {d} | {form} | {b:,.3f} | {j:,.3f} | n/a | {b / j:.2f} |")
    print()


if __name__ == "__main__":
    main()
