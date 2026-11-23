#!/usr/bin/env python3
"""Copies the README's samples from test/Styml.Yaml.Tests/Readme into README.md.

Each region of ReadmeSamples.cs between "// readme: <name> [language]" and "// readme: end" is a sample: its code,
dedented, and, when the marker names a language, the output in Readme/expected/<name>.txt. The README holds each
sample between "<!-- sample:<name> -->" and "<!-- /sample:<name> -->"; this script rewrites those blocks, and
ReadmeDocTests (in the same test project) fails when the README and the samples drift.

    python3 eng/docs/readme.py          # rewrite README.md
    python3 eng/docs/readme.py --check  # exit 1 if README.md is out of date
"""
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
SAMPLES = os.path.join(ROOT, "test", "Styml.Yaml.Tests", "Readme")
README = os.path.join(ROOT, "README.md")


def regions(source):
    """{name: (language or None, code)} from the source's readme regions."""
    found, current, lines = {}, None, []
    for line in source.replace("\r\n", "\n").split("\n"):
        marker = re.match(r"^\s*// readme: ([a-z0-9-]+)(?: ([a-z]+))?\s*$", line)
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


def render(readme, samples, expected_dir):
    def replace(m):
        name = m.group(1)
        if name not in samples:
            raise SystemExit(f"README.md shows sample '{name}', which ReadmeSamples.cs does not define")
        language, code = samples[name]
        expected = open(os.path.join(expected_dir, name + ".txt"), encoding="utf-8").read() if language else ""
        return block(name, language, code, expected)
    return re.sub(r"<!-- sample:([a-z0-9-]+) -->.*?<!-- /sample:\1 -->", replace, readme, flags=re.S)


def main():
    samples = regions(open(os.path.join(SAMPLES, "ReadmeSamples.cs"), encoding="utf-8-sig").read())
    readme = open(README, encoding="utf-8").read()
    updated = render(readme, samples, os.path.join(SAMPLES, "expected"))
    unused = [n for n in samples if f"<!-- sample:{n} -->" not in updated]
    if unused:
        raise SystemExit("ReadmeSamples.cs defines samples the README does not show: " + ", ".join(unused))
    if "--check" in sys.argv:
        sys.exit(0 if updated == readme else "README.md is out of date: run python3 eng/docs/readme.py")
    if updated != readme:
        open(README, "w", encoding="utf-8").write(updated)
        print("updated README.md")


if __name__ == "__main__":
    main()
