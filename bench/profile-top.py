#!/usr/bin/env python3
# M7d (the attribution step): the hottest frames of a dotnet-trace sampled profile,
# exported as Speedscope (dotnet-trace collect --profile dotnet-sampled-thread-time --format Speedscope ...).
# Self samples are the stack's last frame; inclusive samples count each frame once per stack. Only the thread with the
# most samples is read (the benchmark loop's). A method the JIT inlined has no frame of its own: its samples are its
# caller's, which is why the report reads the self column together with the inclusive one.
# Usage: python3 bench/profile-top.py <file.speedscope.json> [count] [filter substring]
import json
import sys
from collections import Counter

path = sys.argv[1]
count = int(sys.argv[2]) if len(sys.argv) > 2 else 25
needle = sys.argv[3] if len(sys.argv) > 3 else None

with open(path) as f:
    data = json.load(f)
frames = [fr["name"] for fr in data["shared"]["frames"]]
self_counts, inclusive = Counter(), Counter()
profile = max(data["profiles"], key=lambda p: len(p.get("samples", p.get("events", []))))
if profile.get("type") == "sampled":
    weights = profile.get("weights") or [1] * len(profile["samples"])
    for stack, weight in zip(profile["samples"], weights):
        if not stack:
            continue
        self_counts[frames[stack[-1]]] += weight
        for name in {frames[i] for i in stack}:
            inclusive[name] += weight
    samples = len(profile["samples"])
else:
    # dotnet-trace's Speedscope export is evented: open and close events with times; each interval between two events
    # is the top frame's own time and every open frame's inclusive time.
    stack, last = [], None
    for event in profile["events"]:
        at = event["at"]
        if last is not None and stack and at > last:
            delta = at - last
            # The leaf is a pseudo-frame (CPU_TIME, UNMANAGED_CODE_TIME): the self time is the frame below it.
            top = len(stack) - 1
            while top > 0 and frames[stack[top]] in ("CPU_TIME", "UNMANAGED_CODE_TIME"):
                top -= 1
            self_counts[frames[stack[top]]] += delta
            for name in {frames[i] for i in stack}:
                inclusive[name] += delta
        last = at
        if event["type"] == "O":
            stack.append(event["frame"])
        elif stack:
            stack.pop()
    samples = len(profile["events"])
total = sum(self_counts.values()) or 1


def short(name):
    return name.replace("System.Text.Yaml.", "").replace("System.", "")[:150]


print(f"{path}: {samples} events on the busiest thread ({profile.get('name', '')})")
print(f"{'self %':>7} {'incl %':>7}  frame")
for name, value in self_counts.most_common():
    if needle and needle not in name:
        continue
    print(f"{100 * value / total:7.2f} {100 * inclusive[name] / total:7.2f}  {short(name)}")
    count -= 1
    if count == 0:
        break
