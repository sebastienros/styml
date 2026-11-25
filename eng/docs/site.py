#!/usr/bin/env python3
"""Copies the samples of the documentation site from test/Styml.Yaml.Tests/Docs into docs/_content/styml/guide.

Each region of a Docs/*.cs file between "// docs: <name> [language]" and "// docs: end" is a sample: its code,
dedented, and, when the marker names a language, the output in Docs/expected/<name>.txt, which the test records
(STYML_DOCS_RECORD). A page holds each sample between "<!-- sample:<name> -->" and "<!-- /sample:<name> -->"; this
script rewrites those blocks, and DocsSiteTests (in the same test project) fails when the pages and the samples drift.
The README's samples are eng/docs/readme.py's.

A page also holds blocks copied from the repository's own documents, between "<!-- include:<name> -->" and
"<!-- /include:<name> -->": the options reference (docs/options.md) and the benchmark panels of the README, which the
benchmark tooling rewrites. Their links are rewritten for the site.

    python3 eng/docs/site.py          # rewrite the pages
    python3 eng/docs/site.py --check  # exit 1 if a page is out of date
"""
import glob
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
SAMPLES = os.path.join(ROOT, "test", "Styml.Yaml.Tests", "Docs")
PAGES = os.path.join(ROOT, "docs", "_content", "styml", "guide")
HELPERS = {"DocSample.cs", "DocsSiteTests.cs"}
RAW = "https://raw.githubusercontent.com/sebastienros/styml/main/"
BLOB = "https://github.com/sebastienros/styml/blob/main/"


def read(*path):
    return open(os.path.join(ROOT, *path), encoding="utf-8").read().replace("\r\n", "\n")


def region(text, start, end):
    i, j = text.index(start), text.index(end)
    return text[i + len(start):j].strip("\n")


def include_options():
    """The options reference of docs/options.md, from its marker to the end, with the README's links made absolute."""
    text = read("docs", "options.md")
    text = text[text.index("<!-- options-reference -->"):].replace("<!-- options-reference -->", "").replace("<!-- /options-reference -->", "")
    return re.sub(r"\]\(\.\./([^)#]+)", lambda m: "](" + BLOB + m.group(1), text).strip("\n")


def include_benchmarks():
    """The charts and the tables of the README's benchmark section, with the charts served from GitHub."""
    text = read("README.md")
    charts = region(text, "<!-- panel:charts -->", "<!-- /panel:charts -->")
    charts = re.sub(r"\]\((docs/benchmarks/charts/[^)]+)\)", lambda m: "](" + RAW + m.group(1) + ")", charts)
    return charts + "\n\n" + region(text, "<!-- panel:pivot -->", "<!-- /panel:pivot -->")


INCLUDES = {"options": include_options, "benchmarks": include_benchmarks}


def regions(source):
    """{name: (language or None, code)} from the source's docs regions."""
    found, current, lines = {}, None, []
    for line in source.replace("\r\n", "\n").split("\n"):
        marker = re.match(r"^\s*// docs: ([a-z0-9-]+)(?: ([a-z]+))?\s*$", line)
        if marker and marker.group(1) != "end":
            current, language, lines = marker.group(1), marker.group(2), []
        elif marker and current:
            indents = [len(l) - len(l.lstrip(" ")) for l in lines if l.strip()]
            cut = min(indents) if indents else 0
            found[current] = (language, "\n".join(l[cut:] if l.strip() else "" for l in lines))
            current = None
        elif current:
            lines.append(line)
    return found


def block(name, language, code, expected):
    text = f"<!-- sample:{name} -->\n```csharp\n{code}\n```\n"
    if language:
        text += f"\n```{language}\n{expected.replace(chr(13) + chr(10), chr(10)).rstrip(chr(10))}\n```\n"
    return text + f"<!-- /sample:{name} -->"


def main():
    samples = {}
    for path in sorted(glob.glob(os.path.join(SAMPLES, "*.cs"))):
        if os.path.basename(path) in HELPERS:
            continue
        for name, sample in regions(open(path, encoding="utf-8-sig").read()).items():
            if name in samples:
                raise SystemExit(f"sample '{name}' is defined twice")
            samples[name] = sample

    shown, stale = set(), []
    for page in sorted(glob.glob(os.path.join(PAGES, "*.md"))):
        text = open(page, encoding="utf-8").read()

        def replace(m):
            name = m.group(1)
            if name not in samples:
                raise SystemExit(f"{os.path.basename(page)} shows sample '{name}', which no Docs/*.cs file defines")
            language, code = samples[name]
            expected = ""
            if language:
                expected = open(os.path.join(SAMPLES, "expected", name + ".txt"), encoding="utf-8").read()
            shown.add(name)
            return block(name, language, code, expected)

        def include(m):
            name = m.group(1)
            if name not in INCLUDES:
                raise SystemExit(f"{os.path.basename(page)} includes '{name}', which site.py does not know")
            return f"<!-- include:{name} -->\n{INCLUDES[name]()}\n<!-- /include:{name} -->"

        updated = re.sub(r"<!-- sample:([a-z0-9-]+) -->.*?<!-- /sample:\1 -->", replace, text, flags=re.S)
        updated = re.sub(r"<!-- include:([a-z0-9-]+) -->.*?<!-- /include:\1 -->", include, updated, flags=re.S)
        if updated != text:
            stale.append(os.path.relpath(page, ROOT))
            if "--check" not in sys.argv:
                open(page, "w", encoding="utf-8").write(updated)
                print("updated " + os.path.relpath(page, ROOT))

    unused = sorted(set(samples) - shown)
    if unused:
        raise SystemExit("samples no page shows: " + ", ".join(unused))
    if "--check" in sys.argv and stale:
        sys.exit("out of date: " + ", ".join(stale) + ": run python3 eng/docs/site.py")


if __name__ == "__main__":
    main()
