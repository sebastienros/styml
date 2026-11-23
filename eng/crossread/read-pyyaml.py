"""Reads the documents of eng/crossread/write-documents.cs with PyYAML's safe loader and reports each string that does
not come back as itself, per setting and position. Each node is constructed on its own, so a string on which PyYAML
fails is reported without stopping the others."""
import json
import os
import sys

import yaml

directory = sys.argv[1] if len(sys.argv) > 1 else "artifacts/crossread"
with open(os.path.join(directory, "strings.json"), encoding="utf-8") as f:
    strings = json.load(f)

failed_on = False
for setting in ("off", "on"):
    for position in ("items", "flow", "keys"):
        with open(os.path.join(directory, f"{setting}.{position}.yaml"), encoding="utf-8") as f:
            text = f.read()
        try:
            roots = [yaml.compose(text, Loader=yaml.SafeLoader)]
        except yaml.YAMLError:
            # The whole document does not parse: one line at a time, and a line that does not parse is a misread.
            roots = []
            for line in text.split("\n")[:-1]:
                try:
                    roots.append(yaml.compose(line, Loader=yaml.SafeLoader))
                except yaml.YAMLError as e:
                    roots.append(e)
        nodes = []
        for root in roots:
            if isinstance(root, Exception):
                nodes.append(root)
            elif position == "keys":
                nodes.extend(k for k, _ in root.value)
            elif position == "flow":
                nodes.extend(n.value[0] for n in root.value)
            else:
                nodes.extend(root.value)
        loader = yaml.SafeLoader("")
        misreads = []
        for i, node in enumerate(nodes):
            try:
                if isinstance(node, Exception):
                    raise node
                value = loader.construct_object(node, deep=True)
                if not isinstance(value, str) or value != strings[i]:
                    misreads.append(f"{strings[i]!r} -> {type(value).__name__} {value!r}")
            except Exception as e:  # noqa: BLE001 - every failure is a misread to report
                misreads.append(f"{strings[i]!r} -> {type(e).__name__}")
        print(f"PyYAML {yaml.__version__}, option {setting}, {position}: {len(nodes)} strings, {len(misreads)} misread")
        for m in misreads[:40]:
            print(f"  {m}")
        failed_on = failed_on or (setting == "on" and bool(misreads))
sys.exit(1 if failed_on else 0)
