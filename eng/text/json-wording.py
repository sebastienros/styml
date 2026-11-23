#!/usr/bin/env python3
# M7c: "JSON" where it means the input format becomes "YAML".
# Applies to src/Styml and src/Common `///` doc lines, the values of src/Styml/Resources/Strings.resx and the three
# trimming-message constants; the tests that hold a changed message's text get the same edit. Recorded in UPSTREAM.md.
#
# The rule: "JSON" stays where it names JSON itself (the JSON-compatible style, Schema = Json and the JSON schema, JSON
# schema export, JSON Lines, JSONPath, System.Text.Json interop, a JSON source or JSON's own syntax), and otherwise
# becomes "YAML". A line that already says "YAML" or carries a step tag (M2 to M7) is Styml's own wording, written to
# mean what it says, and is left alone. The inherited references to RFC 8259 are reworded first.
#
# Usage: python3 eng/text/json-wording.py [--apply] [--base <rev>]   (without --apply: counts and a sample only;
# --base: also derive the tests' message edits from that revision's Strings.resx, once the rename is applied)
import glob
import re
import sys

APPLY = "--apply" in sys.argv

# Reworded first: the inherited references to the JSON RFC.
PHRASES = [
    (" according to the JSON RFC (https://tools.ietf.org/html/rfc8259)", " according to the YAML 1.2 specification (https://yaml.org/spec/1.2.2/)"),
    ("follows the JSON RFC strictly (i.e. comments within the JSON are invalid) and reads", "follows the YAML 1.2 specification and reads"),
    ("that is different from the JSON RFC (for example", "that is different from the defaults (for example"),
    ("structurally valid according to JSON RFC.", "structurally valid."),
    ("an RFC 8259-compliant JSON payload", "a well-formed YAML payload"),
    ("according to the JSON RFC,", "according to the YAML 1.2 specification,"),
    ("according to the JSON RFC", "according to the YAML 1.2 specification"),
    ("the JSON RFC", "the YAML 1.2 specification"),
    ("JSON RFC", "YAML 1.2 specification"),
]

# "JSON" followed by one of these names JSON itself.
KEEP_AFTER = re.compile(r"JSON(?=-compatible|-Compatible| Lines| schema| Schema|'s | source(?! generator)|Path| Path| twin| interop| has no| form)")
KEEP_LINE = re.compile(r"\bYAML\b|\bM[2-7][a-f]?\b|System\.Text\.Json|JsonSerializer|Utf8JsonReader|Utf8JsonWriter|JsonElement|JsonNode|JsonDocument|YamlSchemaKind\.Json|Schema = Json|JsonCompatible")

# Strings.resx values that mean JSON itself, by name.
KEEP_RESOURCES = {
    "YamlSchemaExporter_ReferenceHandlerPreserve_NotSupported",
    "YamlSchemaExporter_DepthTooLarge",
    "YamlGrammar_UnresolvedJsonSchemaScalar",
    "YamlTagWithoutDiscriminator",
    "YamlComplexKeyJsonCompatible",
    "NotSupportedInJsonCompatibleStyle",
    "SpecialNumberValuesNotSupported",  # thrown by the JSON-compatible writer only: block style writes .nan and .inf
}

WORD = re.compile(r"\bJSON\b")


def has_bom(path):
    with open(path, "rb") as f:
        return f.read(3) == b"\xef\xbb\xbf"


def rename(text):
    for old, new in PHRASES:
        text = text.replace(old, new)
    out, last = [], 0
    for m in WORD.finditer(text):
        if KEEP_AFTER.match(text, m.start()):
            continue
        out.append(text[last:m.start()])
        out.append("YAML")
        last = m.end()
    out.append(text[last:])
    return "".join(out)


stats = {"doc lines changed": 0, "doc lines kept (own wording)": 0, "occurrences kept (JSON itself)": 0, "files": 0}
samples, kept_samples = [], []
message_edits = []  # (old fragment, new fragment) for the tests

for path in sorted(glob.glob("src/Styml/**/*.cs", recursive=True) + glob.glob("src/Common/**/*.cs", recursive=True)):
    if "/obj/" in path or "/bin/" in path:
        continue
    with open(path, encoding="utf-8-sig") as f:
        original = f.read()
    lines = original.split("\n")
    changed = False
    for i, line in enumerate(lines):
        stripped = line.lstrip()
        is_doc = stripped.startswith("///")
        is_const = re.search(r'internal const string \w+(UnreferencedCode|DynamicCode)\w* = "JSON serialization', line) is not None
        if not (is_doc or is_const) or not WORD.search(line):
            continue
        if KEEP_LINE.search(line) and not any(old in line for old, _ in PHRASES):
            stats["doc lines kept (own wording)"] += 1
            if len(kept_samples) < 25:
                kept_samples.append(f"{path}:{i + 1}: {stripped[:150]}")
            continue
        new = rename(line)
        stats["occurrences kept (JSON itself)"] += len(WORD.findall(new))
        if new != line:
            stats["doc lines changed"] += 1
            if len(samples) < 40 or "RFC" in line:
                samples.append(f"{path}:{i + 1}\n  - {stripped[:160]}\n  + {new.lstrip()[:160]}")
            lines[i] = new
            changed = True
    if changed:
        stats["files"] += 1
        if APPLY:
            with open(path, "w", encoding="utf-8-sig" if has_bom(path) else "utf-8") as f:
                f.write("\n".join(lines))

# Strings.resx.
resx = "src/Styml/Resources/Strings.resx"
with open(resx, encoding="utf-8-sig") as f:
    content = f.read()
values_changed = values_kept = 0


def fix_value(m):
    global values_changed, values_kept
    name, value = m.group(1), m.group(3)
    if not WORD.search(value):
        return m.group(0)
    if name in KEEP_RESOURCES or re.search(r"\bYAML\b", value):
        values_kept += 1
        return m.group(0)
    new = rename(value)
    if new != value:
        values_changed += 1
        # The literal parts around the format holes, for the tests.
        for old_part, new_part in zip(re.split(r"\{\d+\}", value), re.split(r"\{\d+\}", new)):
            if old_part != new_part and "JSON" in old_part and len(old_part.strip()) > 8:
                message_edits.append((old_part, new_part))
                # An assertion may hold only the part from "JSON" on ("JSON values of type 'Object'").
                tail_old = old_part[old_part.index("JSON"):]
                tail_new = new_part[old_part.index("JSON"):]
                if len(tail_old.strip()) > 12:
                    message_edits.append((tail_old, tail_new))
    whole = m.group(0)
    return whole[:m.start(3) - m.start(0)] + new + whole[m.end(3) - m.start(0):]


content2 = re.sub(r'<data name="([^"]+)"( [^>]*)?>\s*<value>(.*?)</value>', fix_value, content, flags=re.S)
if APPLY:
    with open(resx, "w", encoding="utf-8-sig" if has_bom(resx) else "utf-8") as f:
        f.write(content2)

# With --base <rev>, the message edits are also derived from the values at that revision (the rename already applied).
if "--base" in sys.argv:
    import subprocess
    base = sys.argv[sys.argv.index("--base") + 1]
    old_resx = subprocess.run(["git", "show", f"{base}:{resx}"], capture_output=True, text=True, check=True).stdout
    pattern = r'<data name="([^"]+)"( [^>]*)?>\s*<value>(.*?)</value>'
    current = {m.group(1): m.group(3) for m in re.finditer(pattern, content2, flags=re.S)}
    for m in re.finditer(pattern, old_resx, flags=re.S):
        name, value = m.group(1), m.group(3)
        new = current.get(name)
        if new is None or new == value:
            continue
        for old_part, new_part in zip(re.split(r"\{\d+\}", value), re.split(r"\{\d+\}", new)):
            if old_part != new_part and "JSON" in old_part:
                message_edits.append((old_part, new_part))
                i = old_part.index("JSON")
                if len(old_part[i:].strip()) > 12:
                    message_edits.append((old_part[i:], new_part[i:]))

# The tests that hold a changed message's text.
test_edits = 0
for path in sorted(glob.glob("test/**/*.cs", recursive=True)):
    # The generator's diagnostics have their own resources, which this rename does not change.
    if "/obj/" in path or "/bin/" in path or "SourceGeneration.Unit.Tests" in path or path.endswith("UnionTests.cs"):
        continue
    with open(path, encoding="utf-8-sig") as f:
        text = f.read()
    new = text
    for old_part, new_part in message_edits:
        # Assertions hold a message's literal parts; only the parts long enough to be specific are replaced.
        for old_sub, new_sub in [(old_part.strip(), new_part.strip())]:
            if len(old_sub) > 12 and old_sub in new:
                new = new.replace(old_sub, new_sub)
    if new != text:
        test_edits += 1
        print("test file:", path)
        if APPLY:
            with open(path, "w", encoding="utf-8-sig" if has_bom(path) else "utf-8") as f:
                f.write(new)

print(stats, {"resx values changed": values_changed, "resx values kept": values_kept, "test files": test_edits})
if "--kept" in sys.argv:
    for path in sorted(glob.glob("src/Styml/**/*.cs", recursive=True) + glob.glob("src/Common/**/*.cs", recursive=True)):
        if "/obj/" in path or "/bin/" in path:
            continue
        for i, line in enumerate(open(path, encoding="utf-8-sig")):
            if line.lstrip().startswith("///") and WORD.search(line):
                after = line if KEEP_LINE.search(line) else rename(line)
                if WORD.search(after):
                    print(f"KEPT {path}:{i + 1}: {after.strip()[:170]}")
    sys.exit(0)
if not APPLY:
    print("\n".join(samples))
    print("--- kept (own wording), sample:")
    print("\n".join(kept_samples))
