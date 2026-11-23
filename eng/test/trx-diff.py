#!/usr/bin/env python3
# The per-method gate of M7a and M7c: compares two gate
# runs (artifacts/m5/<run>/<leg>/*.trx, from eng/test/m5-gate.sh) method by method. A method's outcome is the worst of
# its cases (Failed > NotExecuted > Passed), keyed by test class and method name, so a theory counts once.
# Usage: python3 eng/test/trx-diff.py <before run dir> <after run dir> [--expect-gone FILE] [--expect-new FILE]
#                                      [--expect-fewer FILE]
#   --expect-gone: methods ("Class.Method" or "Method" per line) allowed to disappear (the retired ones);
#   --expect-new: methods allowed to appear, and required to pass (the retargeted ones, which no leg ran before);
#   --expect-fewer: kept methods allowed to run fewer cases (retired rows), by the same names.
# Prints every difference, every kept method whose case count changed (M7c: a method
# that keeps its outcome can still lose cases), and exits 1 if one is not expected: an outcome change, a method gone or
# new that is not listed, or a decrease in cases that is not listed.
import glob
import os
import sys
import xml.etree.ElementTree as ET

NS = {"t": "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"}
RANK = {"Passed": 0, "NotExecuted": 1, "Failed": 2}


def load(run):
    """{(leg, project): {Class.Method: (outcome, cases)}}"""
    out = {}
    for path in glob.glob(os.path.join(run, "*", "*.trx")):
        leg = os.path.basename(os.path.dirname(path))
        project = os.path.basename(path).split(".net")[0]
        root = ET.parse(path).getroot()
        methods = {}
        for test in root.iterfind(".//t:UnitTest", NS):
            m = test.find("t:TestMethod", NS)
            methods[test.get("id")] = m.get("className").split(",")[0] + "." + m.get("name")
        result = {}
        for r in root.iterfind(".//t:UnitTestResult", NS):
            key = methods.get(r.get("testId"))
            if key is None:
                continue
            outcome = r.get("outcome")
            best, cases = result.get(key, ("Passed", 0))
            result[key] = (outcome if RANK.get(outcome, 2) > RANK.get(best, 2) else best, cases + 1)
        out[(leg, project)] = result
    return out


def names(path):
    if not path:
        return set()
    with open(path, encoding="utf-8") as f:
        return {line.strip() for line in f if line.strip() and not line.startswith("#")}


def main():
    args = sys.argv[1:]
    gone_ok = names(args[args.index("--expect-gone") + 1]) if "--expect-gone" in args else set()
    new_ok = names(args[args.index("--expect-new") + 1]) if "--expect-new" in args else set()
    fewer_ok = names(args[args.index("--expect-fewer") + 1]) if "--expect-fewer" in args else set()
    before, after = load(args[0]), load(args[1])
    bad = 0
    counts = {"same": 0, "gone (expected)": 0, "new (expected, passed)": 0}
    cases = {"fewer cases (expected)": 0, "fewer cases (not expected)": 0, "more cases": 0}
    case_delta = 0
    for key in sorted(set(before) | set(after)):
        b, a = before.get(key, {}), after.get(key, {})
        for method in sorted(set(b) | set(a)):
            label = f"{key[0]} {key[1]} {method}"
            if method in b and method in a:
                if b[method][0] != a[method][0]:
                    print(f"CHANGED  {label}: {b[method][0]} -> {a[method][0]}")
                    bad += 1
                else:
                    counts["same"] += 1
                before_cases, after_cases = b[method][1], a[method][1]
                if before_cases != after_cases:
                    case_delta += after_cases - before_cases
                    if after_cases > before_cases:
                        cases["more cases"] += 1
                        print(f"CASES    {label}: {before_cases} -> {after_cases}")
                    elif method in fewer_ok or ".".join(method.split(".")[-2:]) in fewer_ok or method.split(".")[-1] in fewer_ok:
                        cases["fewer cases (expected)"] += 1
                        print(f"CASES    {label}: {before_cases} -> {after_cases} (expected)")
                    else:
                        cases["fewer cases (not expected)"] += 1
                        print(f"CASES    {label}: {before_cases} -> {after_cases} (fewer, not expected)")
                        bad += 1
            elif method in b:
                # A method's short name may be listed without its class's namespace.
                if method in gone_ok or ".".join(method.split(".")[-2:]) in gone_ok or method.split(".")[-1] in gone_ok:
                    counts["gone (expected)"] += 1
                else:
                    print(f"GONE     {label} (was {b[method][0]})")
                    bad += 1
            else:
                expected = method in new_ok or ".".join(method.split(".")[-2:]) in new_ok or method.split(".")[-1] in new_ok
                if expected and a[method][0] == "Passed":
                    counts["new (expected, passed)"] += 1
                else:
                    print(f"NEW      {label}: {a[method][0]}" + ("" if expected else " (not expected)"))
                    bad += 1 if not expected or a[method][0] != "Passed" else 0
    print(", ".join(f"{v} {k}" for k, v in counts.items()) + f"; {bad} unexpected")
    print("cases of kept methods: " + ", ".join(f"{v} {k}" for k, v in cases.items()) + f"; net {case_delta:+d} cases")
    sys.exit(1 if bad else 0)


if __name__ == "__main__":
    main()
