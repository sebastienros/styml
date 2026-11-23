#!/usr/bin/env python3
# M7d (the attribution step): the hottest methods of an xctrace Time Profiler trace of a .NET process, symbolised with the
# runtime's perf map. dotnet-trace's sampled profile misattributes some paths on this machine (M6a; item 3's
# serialize profile gave 95% to MemmoveNative), and Instruments cannot name JIT-compiled frames; the perf map can.
#   xcrun xctrace record --template 'Time Profiler' --output t.trace --env DOTNET_PerfMapEnabled=1 --launch -- dotnet ...
#   xcrun xctrace export --input t.trace --xpath '/trace-toc/run[@number="1"]/data/table[@schema="time-profile"]' > t.xml
#   cp /tmp/perf-<pid>.map t.perf.map
# Self time is the sample's leaf frame; inclusive counts each method once per backtrace.
# Usage: python3 bench/xctrace-top.py <t.xml> <t.perf.map> [count] [filter substring | callers:<leaf> | offsets:<method>]
import bisect
import sys
import xml.etree.ElementTree as ET
from collections import Counter

xml_path, map_path = sys.argv[1], sys.argv[2]
count = int(sys.argv[3]) if len(sys.argv) > 3 else 30
needle = sys.argv[4] if len(sys.argv) > 4 else None

starts, ends, names = [], [], []
entries = []
for line in open(map_path, errors="replace"):
    parts = line.rstrip("\n").split(" ", 2)
    if len(parts) < 3:
        continue
    try:
        start, size = int(parts[0], 16), int(parts[1], 16)
    except ValueError:
        continue
    entries.append((start, start + size, parts[2]))
entries.sort()
starts = [e[0] for e in entries]


def resolve(frame):
    name = frame.get("name") or ""
    addr = frame.get("addr")
    if addr and (not name or name.startswith("0x")):
        a = int(addr, 16)
        i = bisect.bisect_right(starts, a) - 1
        if i >= 0 and entries[i][0] <= a < entries[i][1]:
            return entries[i][2]
        return name or addr
    return name


root = ET.parse(xml_path).getroot()
ids = {el.get("id"): el for el in root.iter() if el.get("id")}
self_counts, inclusive = Counter(), Counter()
total = 0
for row in root.iter("row"):
    bt = row.find("backtrace")
    if bt is None:
        continue
    if bt.get("ref"):
        bt = ids[bt.get("ref")]
    frames = [ids[f.get("ref")] if f.get("ref") else f for f in bt.findall("frame")]
    if not frames:
        continue
    resolved = [resolve(f) for f in frames]
    self_counts[resolved[0]] += 1
    for name in set(resolved):
        inclusive[name] += 1
    total += 1

if needle and needle.startswith("offsets:"):
    # Where in a method's code its own samples fall: the leaf address less the method's start, in 16-byte buckets, for
    # the leaf frames whose name contains the text after "offsets:" (each compiled body apart). A JitDisasm dump of the
    # same body (DOTNET_JitStdOutFile) gives each instruction group's offset.
    target = needle[len("offsets:"):]
    buckets = {}
    for row in root.iter("row"):
        bt = row.find("backtrace")
        if bt is None:
            continue
        if bt.get("ref"):
            bt = ids[bt.get("ref")]
        frames = [ids[f.get("ref")] if f.get("ref") else f for f in bt.findall("frame")]
        if not frames or not frames[0].get("addr"):
            continue
        a = int(frames[0].get("addr"), 16)
        i = bisect.bisect_right(starts, a) - 1
        if i < 0 or not (entries[i][0] <= a < entries[i][1]) or target not in entries[i][2]:
            continue
        body = (entries[i][2], entries[i][1] - entries[i][0])
        buckets.setdefault(body, Counter())[(a - entries[i][0]) // 16 * 16] += 1
    for (name, size), counter in sorted(buckets.items(), key=lambda kv: -sum(kv[1].values())):
        total_body = sum(counter.values())
        print(f"{100 * total_body / total:6.2f}%  size 0x{size:x}  {name[:140]}")
        for offset, value in sorted(counter.items()):
            if value * 100 >= total_body * 2:
                print(f"    +0x{offset:04x}  {100 * value / total:5.2f}%  {'#' * max(1, round(60 * value / total_body))}")
    sys.exit(0)

if needle and needle.startswith("callers:"):
    # The immediate callers of the leaf frames whose name contains the text after "callers:".
    target = needle[len("callers:"):]
    callers = Counter()
    for row in root.iter("row"):
        bt = row.find("backtrace")
        if bt is None:
            continue
        if bt.get("ref"):
            bt = ids[bt.get("ref")]
        frames = [ids[f.get("ref")] if f.get("ref") else f for f in bt.findall("frame")]
        resolved = [resolve(f) for f in frames]
        if resolved and target in resolved[0]:
            callers[resolved[1] if len(resolved) > 1 else "(none)"] += 1
    print(f"{xml_path}: {total} samples; callers of leaf '{target}'")
    for name, value in callers.most_common(count):
        print(f"{100 * value / total:7.2f}  {name[:150]}")
    sys.exit(0)

print(f"{xml_path}: {total} samples")
print(f"{'self %':>7} {'incl %':>7}  method")
for name, value in self_counts.most_common():
    if needle and needle not in name:
        continue
    print(f"{100 * value / total:7.2f} {100 * inclusive[name] / total:7.2f}  {name[:150]}")
    count -= 1
    if count == 0:
        break
