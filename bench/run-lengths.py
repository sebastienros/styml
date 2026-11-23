#!/usr/bin/env python3
# M7d: the run lengths the reader's scans skip, per document, so that a
# vector path is attempted only where runs are long. A counting pass, not a sitting.
#   quoted:  the body of each quoted scalar, up to its first escape or the closing quote (item 1b's run);
#   plain:   each run of plain-scalar content between the bytes the plain scan acts on (item 1a's run);
#   indent:  each line's leading spaces (item 1c);
#   blanks:  each run of spaces or tabs between tokens on a line (item 1c).
# It is a lexical approximation of the reader (comments and block scalars are skipped by simple rules), close enough
# for medians and 90th percentiles.
# Usage: python3 bench/run-lengths.py [documents...]   (default: the Tokenize rows' documents)
import re
import statistics
import sys

DOCS = sys.argv[1:] or ["k8s-m.yaml", "compose-m.yaml", "openapi-m.yaml", "k8s-m.json", "records-l.min.json"]
PLAIN_STOP = set(b": \t\r\n")
FLOW_STOP = PLAIN_STOP | set(b",[]{}")


def pct(values, p):
    if not values:
        return 0
    values = sorted(values)
    return values[min(len(values) - 1, int(p / 100 * len(values)))]


def summary(name, values):
    if not values:
        return f"{name}: none"
    return f"{name}: n={len(values)}, median={statistics.median(values):g}, p90={pct(values, 90)}, mean={statistics.mean(values):.1f}"


for doc in DOCS:
    data = open(f"bench/corpus/{doc}", "rb").read()
    quoted, plain, indent, blanks = [], [], [], []
    for line in data.split(b"\n"):
        stripped = line.lstrip(b" ")
        if stripped:
            indent.append(len(line) - len(stripped))
    i, n = 0, len(data)
    flow = 0
    while i < n:
        b = data[i]
        if b in (0x22, 0x27):  # a quoted scalar
            j = i + 1
            while j < n and data[j] != b and not (b == 0x22 and data[j] == 0x5C) and data[j] not in (10, 13):
                j += 1
            quoted.append(j - i - 1)
            # Skip to the closing quote.
            j = i + 1
            while j < n and data[j] != b:
                j += 2 if (b == 0x22 and data[j] == 0x5C) else 1
            i = j + 1
            continue
        if b == 0x23 and (i == 0 or data[i - 1] in (32, 9, 10)):  # a comment
            while i < n and data[i] != 10:
                i += 1
            continue
        if b in (32, 9):
            j = i
            while j < n and data[j] in (32, 9):
                j += 1
            if i > 0 and data[i - 1] != 10:
                blanks.append(j - i)
            i = j
            continue
        if b in (10, 13):
            i += 1
            continue
        if b in b"[{":
            flow += 1
            i += 1
            continue
        if b in b"]}":
            flow = max(0, flow - 1)
            i += 1
            continue
        if b in b",":
            i += 1
            continue
        stop = FLOW_STOP if flow else PLAIN_STOP
        j = i
        while j < n and data[j] not in stop:
            j += 1
        if j > i:
            plain.append(j - i)
            i = j
        else:
            i += 1
    print(f"{doc} ({n} bytes)")
    for name, values in (("quoted", quoted), ("plain", plain), ("indent", indent), ("blanks", blanks)):
        print("  " + summary(name, values))
