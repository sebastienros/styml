"""Regenerates the benchmark charts (docs/benchmarks/charts/*.svg and .png) from bench/run-panel.sh's results.

Adapted from the chart generator of Fluid (github.com/sebastienros/fluid): one chart per scenario, on one document,
with a duration panel and a memory-allocation panel, one bar per library, Styml's emphasised and System.Text.Json's
in grey as the reference. From the repository root:

    sh bench/run-panel.sh
    python3 bench/charts/generate_charts.py --readme README.md --page docs/benchmarks/README.md

--readme and --page rewrite the block between "<!-- panel:charts -->" and "<!-- /panel:charts -->" in those files with
the charts' images and their alt text (the README shows three charts, the benchmarks page all five).

The SVGs are filled from benchmark-chart.svg.template. The PNGs are rendered at twice the SVG's size with Chromium
through playwright-cli, as assets/render.py renders the artwork (pass --no-png to write the SVGs only).
"""
import argparse
import json
import math
import os
import re
import subprocess
import tempfile

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))
RESULTS = os.path.join(ROOT, "bench", "results", "Panel", "net11.0", "results")
OUTPUT = os.path.join(ROOT, "docs", "benchmarks", "charts")
DOCUMENT, DOCUMENT_NAME = "k8s-m", "Kubernetes manifests, k8s-m, 76 KB"

WIDTH, HEIGHT = 1051, 452
TOP, BOTTOM = 84, 342
PLOT_HEIGHT = BOTTOM - TOP

# Styml in the artwork's indigo; the other libraries in the Okabe-Ito palette (distinct under every common colour
# vision deficiency); System.Text.Json in grey, as the reference rather than a rival.
STYML, STYML_ALT, STJ = "#4F46E5", "#A5B4FC", "#9CA3AF"
LIBRARIES = {"VYaml": "#E69F00", "SharpYaml": "#009E73", "YamlDotNet": "#D55E00"}

# Each series: (benchmark method, label, colour, emphasised).
def yaml_libraries(*names):
    return [(n, n, LIBRARIES[n], False) for n in names]

CHARTS = [
    {
        "file": "deserialize-sourcegen", "report": "DeserializeSourceGen", "readme": True, "title": "Deserialize, source-generated",
        "series": [("Styml_Yaml", "Styml", STYML, True)] + yaml_libraries("VYaml", "SharpYaml", "YamlDotNet")
                  + [("Stj", "System.Text.Json (JSON)", STJ, False)],
        "caption": ["UTF-8 bytes to the object model through each library's generated contract. System.Text.Json reads the same data as JSON."],
    },
    {
        "file": "deserialize-reflection", "report": "DeserializeReflection", "title": "Deserialize, reflection",
        "series": [("Styml_Yaml", "Styml", STYML, True)] + yaml_libraries("SharpYaml", "YamlDotNet")
                  + [("Stj", "System.Text.Json (JSON)", STJ, False)],
        "caption": ["VYaml has no reflection serializer. System.Text.Json reads the same data as JSON."],
    },
    {
        "file": "serialize-sourcegen", "report": "SerializeYamlSourceGen", "readme": True, "title": "Serialize, source-generated",
        "series": [("Styml_Block", "Styml", STYML, True), ("Styml_Block_Writer", "Styml, reused buffer", STYML_ALT, True)]
                  + yaml_libraries("VYaml", "SharpYaml", "YamlDotNet") + [("Stj", "System.Text.Json (JSON)", STJ, False)],
        "log": True,
        "caption": ["Styml's first bar returns a new byte[]; its reused-buffer bar writes into a reused writer and buffer, as SharpYaml's",
                    "and VYaml's rows do (SharpYaml's buffer is UTF-16). YamlDotNet returns a string. System.Text.Json writes JSON."],
    },
    {
        "file": "dom-parse", "report": "DomParse", "readme": True, "title": "DOM parse",
        "series": [("Styml_Yaml", "Styml YamlDocument", STYML, True), ("Styml_Node", "Styml YamlNode", STYML_ALT, True)]
                  + yaml_libraries("SharpYaml", "YamlDotNet")
                  + [("Stj", "JsonDocument (JSON)", STJ, False), ("Stj_Node", "JsonNode (JSON)", "#D1D5DB", False)],
        "caption": ["VYaml has no DOM. YamlDocument and JsonDocument rent their storage from the array pool and return it on dispose;",
                    "YamlNode and JsonNode are the mutable models. YamlDotNet and SharpYaml build their YamlStream."],
    },
    {
        "file": "tokenize", "report": "Tokenize", "title": "Tokenize (the reader alone)",
        "series": [("Styml_Yaml", "Styml", STYML, True)] + yaml_libraries("VYaml", "SharpYaml", "YamlDotNet")
                  + [("Stj", "System.Text.Json (JSON)", STJ, False)],
        "caption": ["Every token to the end. YamlDotNet and SharpYaml read a string decoded in setup; the others read UTF-8 bytes."],
    },
]


def parse_report(path, document):
    """Returns {method: (microseconds, bytes)} for one document of a BenchmarkDotNet GitHub markdown report."""
    time_units = {"ns": 0.001, "us": 1, "μs": 1, "ms": 1000, "s": 1_000_000}
    size_units = {"B": 1, "KB": 1024, "MB": 1024 * 1024}
    rows, header = {}, None
    with open(path, encoding="utf-8") as f:
        for line in f:
            cells = [c.strip().replace("**", "") for c in line.strip().strip("|").split("|")]
            if cells[0] == "Method":
                header = cells
                continue
            if header is None or len(cells) != len(header) or not cells[0] or cells[0].startswith("-"):
                continue
            row = dict(zip(header, cells))
            if row.get("Document") != document:
                continue
            mean, unit = row["Mean"].split()
            alloc = row["Allocated"]
            size = 0.0 if alloc in ("-", "0 B") else float(alloc.split()[0].replace(",", "")) * size_units[alloc.split()[1]]
            rows[row["Method"]] = (float(mean.replace(",", "")) * time_units[unit], size)
    return rows


def fmt(v):
    return f"{round(v, 1):g}"


def fmt_bytes(b):
    if b == 0:
        return "0 B"
    if b < 1024:
        return f"{b:,.0f} B"
    if b < 1024 * 1024:
        return f"{b / 1024:,.1f} KB"
    return f"{b / 1024 / 1024:,.2f} MB"


def nice_top(peak):
    """The smallest of 1, 2, 2.5, 5 times a power of ten at or above the peak, with room for a value label."""
    peak *= 1.08
    exponent = math.floor(math.log10(peak))
    for m in (1, 2, 2.5, 5, 10):
        if m * 10 ** exponent >= peak:
            return m * 10 ** exponent


def axis(x0, values, log, unit_label):
    """Returns the grid lines and a function mapping a value to a bar height."""
    peak = max(values)
    if log:
        positive = [v for v in values if v > 0]
        low = 10 ** math.floor(math.log10(min(positive)))
        high = 10 ** math.ceil(math.log10(peak * 1.15))
        decades = math.log10(high / low)
        ticks = [low * 10 ** i for i in range(int(round(decades)) + 1)]
        height = lambda v: 0 if v <= low else PLOT_HEIGHT * math.log10(v / low) / decades
    else:
        top = nice_top(peak)
        ticks = [top * i / 5 for i in range(6)]
        height = lambda v: PLOT_HEIGHT * v / top
    lines = []
    for t in ticks:
        y = BOTTOM - height(t)
        lines.append(f'    <line class="grid" x1="{x0}" y1="{fmt(y)}" x2="{x0 + 438}" y2="{fmt(y)}"/>'
                     f'<text class="tick" x="{x0 - 10}" y="{fmt(y + 4)}" text-anchor="end">{unit_label(t)}</text>')
    lines.append(f'    <line class="axis" x1="{x0}" y1="{TOP}" x2="{x0}" y2="{BOTTOM}"/>'
                 f'<line class="axis" x1="{x0}" y1="{BOTTOM}" x2="{x0 + 438}" y2="{BOTTOM}"/>')
    return lines, height


def panel(x0, entries, log, title, value_label, tick_label):
    """Draws the bars of entries, a list of (value, colour, emphasised)."""
    lines, height = axis(x0, [v for v, _, _ in entries], log, tick_label)
    lines.append("")
    # Four bars are 72 wide every 105 units; more bars keep the margins and the proportions (Fluid's layout).
    spacing = 105 if len(entries) <= 4 else round(387 / (len(entries) - 1 + 72 / 105), 1)
    width = 72 if len(entries) <= 4 else round(spacing * 72 / 105, 1)
    for i, (value, color, emphasised) in enumerate(entries):
        x = round(x0 + 26 + i * spacing, 1)
        h = max(height(value), 0)
        y = BOTTOM - h
        if emphasised:
            lines.append(f'    <rect x="{fmt(x)}" y="{fmt(y)}" width="{fmt(width)}" height="{fmt(max(h, 1.5))}" rx="1" fill="{color}" stroke="#312e81" stroke-width="2"/>')
            css = "styml-value"
        else:
            lines.append(f'    <rect class="competitor" x="{fmt(x)}" y="{fmt(y)}" width="{fmt(width)}" height="{fmt(max(h, 1))}" rx="1" fill="{color}"/>')
            css = "value"
        lines.append(f'    <text class="{css}" x="{fmt(x + width / 2)}" y="{round(y - 7)}">{value_label(value)}</text>')
    scale = " · logarithmic scale" if log else ""
    lines.append(f'    <text class="axis-title" x="{x0 + 219}" y="381" text-anchor="middle">{title}{scale}</text>')
    return "\n".join(lines)


def legend(items):
    """Draws the legend of items, a list of (name, colour, emphasised), centred."""
    widths = [len(n) * 6.6 + (6 if emphasised else 0) for n, _, emphasised in items]
    gap = 34
    x = (WIDTH - sum(widths) - 11 * len(items) - gap * (len(items) - 1)) / 2
    lines = []
    for (name, color, emphasised), w in zip(items, widths):
        cx = round(x + 5)
        if emphasised:
            lines.append(f'    <circle cx="{cx}" cy="23" r="5" fill="{color}" stroke="#312e81" stroke-width="2"/>')
            lines.append(f'    <text x="{cx + 11}" y="27" font-weight="700">{name}</text>')
        else:
            lines.append(f'    <circle cx="{cx}" cy="23" r="5" fill="{color}" opacity=".8"/>')
            lines.append(f'    <text x="{cx + 11}" y="27">{name}</text>')
        x += 11 + w + gap
    return "\n".join(lines)


def describe(chart, rows, series):
    """The alt text: what the chart compares and the gist of its numbers."""
    styml_time, styml_bytes = rows[series[0][0]]
    parts = []
    for method, label, _, _ in series[1:]:
        t, b = rows[method]
        parts.append(f"{label} {t / styml_time:.2f} times Styml's time ({t:,.1f} µs) and {fmt_bytes(b)}")
    return (f"{chart['title']} of {DOCUMENT_NAME}: Styml takes {styml_time:,.1f} µs and allocates {fmt_bytes(styml_bytes)}; "
            + "; ".join(parts) + ". Smaller is better.")


def alt_text(chart, rows):
    """A short alt text: Styml's numbers and each other bar's time as a multiple of Styml's."""
    series = [s for s in chart["series"] if s[0] in rows]
    t0, b0 = rows[series[0][0]]
    others = ", ".join(f"{label} {rows[m][0] / t0:.2f}×" for m, label, _, _ in series[1:])
    return (f"{chart['title']}, k8s-m: {series[0][1]} {t0:,.0f} µs and {fmt_bytes(b0)} allocated; "
            f"time against it: {others}. Smaller is better.")


def write_block(path, text):
    content = open(path, encoding="utf-8").read()
    start, end = "<!-- panel:charts -->", "<!-- /panel:charts -->"
    a, b = content.index(start) + len(start), content.index(end)
    with open(path, "w", encoding="utf-8") as f:
        f.write(content[:a] + "\n" + text + "\n" + content[b:])
    print("updated", path)


def render_chart(template, chart, rows):
    series = [s for s in chart["series"] if s[0] in rows]
    durations = [(rows[m][0], c, e) for m, _, c, e in series]
    allocations = [(rows[m][1] / 1024, c, e) for m, _, c, e in series]  # the axis is in kilobytes
    log = chart.get("log", False)
    caption = "\n".join(f'  <text class="caption" x="525.5" y="{412 + 16 * i}" text-anchor="middle">{line}</text>'
                        for i, line in enumerate(chart["caption"]))
    values = {
        "title": f"{chart['title']}, {DOCUMENT_NAME}",
        "desc": describe(chart, rows, series),
        "legend": legend([(label, c, e) for _, label, c, e in series]),
        "heading": f"{chart['title']} · {DOCUMENT_NAME} · smaller is better ↓",
        "duration": panel(62, durations, log, "Duration (microseconds)", lambda v: f"{v:,.1f}",
                          lambda t: f"{t:,.0f}" if t >= 1 else f"{t:g}"),
        "memory": panel(588, allocations, log, "Memory allocation (kilobytes)", lambda kb: fmt_bytes(kb * 1024),
                        lambda t: f"{t:,.0f}" if t >= 1 or t == 0 else f"{t:g}"),
        "caption": caption,
    }
    return re.sub(r"\{\{(\w+)\}\}", lambda m: values[m.group(1)], template), values["desc"]


def render_pngs(svgs):
    """Renders each (svg path, png path) at twice the SVG's size, with Chromium through playwright-cli."""
    with tempfile.TemporaryDirectory(prefix="styml-charts-") as work:
        session = os.path.basename(work)
        def run(*args):
            result = subprocess.run(["playwright-cli", f"-s={session}", *args], cwd=work, capture_output=True, text=True)
            if result.returncode or "### Error" in result.stdout:
                raise RuntimeError(f"playwright-cli {args[0]} failed:\n{result.stdout}\n{result.stderr}")
        run("open", "--browser", "chromium")
        try:
            for svg_path, png_path in svgs:
                svg = open(svg_path, encoding="utf-8").read()
                code = (
                    "async browserPage => {"
                    "const context = await browserPage.context().browser().newContext({"
                    f"viewport:{{width:{WIDTH},height:{HEIGHT}}},deviceScaleFactor:2}});"
                    "try {"
                    "const page = await context.newPage();"
                    f"await page.setContent({json.dumps(svg)});"
                    "await page.addStyleTag({content:'body{margin:0}svg{display:block}'});"
                    "await page.evaluate(() => document.fonts.ready);"
                    f"await page.locator('svg').screenshot({{path:{json.dumps(png_path)},scale:'device'}});"
                    "} finally { await context.close(); }}"
                )
                run("run-code", code)
        finally:
            run("close")


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--results", default=RESULTS, help="the panel's BenchmarkDotNet results folder")
    parser.add_argument("--output", default=OUTPUT, help="directory receiving the charts")
    parser.add_argument("--no-png", action="store_true", help="only write the SVGs")
    parser.add_argument("--readme", help="a file whose charts block takes the README's three charts")
    parser.add_argument("--page", help="a file whose charts block takes all five charts")
    args = parser.parse_args()
    template = open(os.path.join(HERE, "benchmark-chart.svg.template"), encoding="utf-8").read()
    os.makedirs(args.output, exist_ok=True)
    pngs, images = [], []
    for chart in CHARTS:
        rows = parse_report(os.path.join(args.results, f"Styml.Benchmarks.{chart['report']}-report-github.md"), DOCUMENT)
        svg, desc = render_chart(template, chart, rows)
        svg_path = os.path.join(args.output, chart["file"] + ".svg")
        with open(svg_path, "w", encoding="utf-8", newline="\n") as f:
            f.write(svg)
        pngs.append((svg_path, os.path.join(args.output, chart["file"] + ".png")))
        print(f"{chart['file']}: {desc}")
        images.append((chart, alt_text(chart, rows)))
    if not args.no_png:
        render_pngs(pngs)
        print(f"Rendered {len(pngs)} PNGs into {args.output}")
    for path, prefix, wanted in [(args.readme, "docs/benchmarks/charts/", lambda c: c.get("readme")), (args.page, "charts/", lambda c: True)]:
        if path:
            write_block(path, "\n\n".join(f"![{alt}]({prefix}{c['file']}.png)" for c, alt in images if wanted(c)))


if __name__ == "__main__":
    main()
