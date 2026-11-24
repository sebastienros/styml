#!/usr/bin/env python3
"""Styml rename tool, Pass A (textual, case-sensitive).

Turns the verbatim System.Text.Json import into System.Text.Yaml / Yaml* names. Run from the repo root on a
tree that is exactly the import commit (eng/import), then commit the result unedited:

    python3 -I eng/rename/rename.py            # rewrite in place
    python3 -I eng/rename/rename.py --check    # dry run: report what would change, write nothing
    python3 -I eng/rename/rename.py --update-counts   # record new expected_counts in protect.json instead of failing

Order (M1):
  1. 'System.Text.Json' -> 'System.Text.Yaml'
  2. protected tokens (rules in protect.json) -> placeholders
  3. 'Json' -> 'Yaml'
  4. generator diagnostic ids SYSLIB1xxx -> STYML1xxx (STJ generator ranges only)
  5. placeholders restored; the run fails if any protected-token count differs before/after
  6. targeted fix-ups (fixups in protect.json), e.g. generator identity in source-generator baselines
File and directory names get steps 1 and 3. Everything is driven by protect.json so that the tool can be
replayed on a newer upstream import. expected_counts are the counts at the upstream commit in
eng/upstream/baseline.json; eng/upstream/sync replays the target commit with --update-counts and shows the changes.
"""
import json
import os
import re
import sys
from fnmatch import fnmatch

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))
CONFIG = os.path.join(os.path.dirname(__file__), 'protect.json')

PLACEHOLDER = '§§{}§§'          # §§n§§ never contains 'Json'
PLACEHOLDER_RE = re.compile('§§(\\d+)§§')


def load_config():
    with open(CONFIG, encoding='utf-8') as f:
        return json.load(f)


def rel(path):
    return os.path.relpath(path, ROOT).replace(os.sep, '/')


def matches_any(relpath, globs):
    return any(fnmatch(relpath, g) for g in globs)


def collect_files(cfg):
    out = []
    for top in cfg['roots']:
        for dirpath, dirnames, filenames in os.walk(os.path.join(ROOT, top)):
            dirnames[:] = sorted(d for d in dirnames if d not in ('bin', 'obj'))
            for name in sorted(filenames):
                p = os.path.join(dirpath, name)
                r = rel(p)
                if os.path.splitext(name)[1].lower() not in cfg['text_extensions']:
                    continue
                if matches_any(r, cfg['content_exclude']):
                    continue
                out.append(p)
    return out


def rules_for(cfg, relpath, text):
    """Protection regexes that apply to this file (global + file-scoped by a trigger regex)."""
    pats = list(cfg['protect']['global'])
    for scope in cfg['protect']['scoped']:
        if re.search(scope['when'], text) and matches_any(relpath, scope.get('files', ['*'])):
            pats.extend(scope['tokens'])
    return [re.compile(p) for p in pats]


def rename_text(cfg, relpath, text, stats):
    original = text
    # 1.
    text = text.replace('System.Text.Json', 'System.Text.Yaml')
    # 2.
    rules = rules_for(cfg, relpath, text)
    before = [len(r.findall(original.replace('System.Text.Json', 'System.Text.Yaml'))) for r in rules]
    saved = []

    def stash(m):
        saved.append(m.group(0))
        return PLACEHOLDER.format(len(saved) - 1)

    for r in rules:
        text = r.sub(stash, text)
    # 3.
    text = text.replace('Json', 'Yaml')
    # 4.
    text = re.sub(cfg['diagnostic_ids']['pattern'], cfg['diagnostic_ids']['replacement'], text)
    # 5.
    text = PLACEHOLDER_RE.sub(lambda m: saved[int(m.group(1))], text)
    after = [len(r.findall(text)) for r in rules]
    for r, b, a in zip(rules, before, after):
        if b != a:
            raise SystemExit(f'{relpath}: protected token count changed for /{r.pattern}/: {b} -> {a}')
        if b:
            stats[r.pattern] = stats.get(r.pattern, 0) + b
    # 6.
    for fx in cfg['fixups']:
        if matches_any(relpath, fx['files']):
            text, n = re.subn(fx['pattern'], fx['replacement'], text)
            stats['fixup: ' + fx['name']] = stats.get('fixup: ' + fx['name'], 0) + n
    return text, text != original


def rename_path_component(name):
    return name.replace('System.Text.Json', 'System.Text.Yaml').replace('Json', 'Yaml')


def main():
    check = '--check' in sys.argv
    update_counts = '--update-counts' in sys.argv
    cfg = load_config()
    stats = {}
    changed = 0

    files = collect_files(cfg)
    for p in files:
        r = rel(p)
        with open(p, encoding='utf-8', newline='') as f:
            text = f.read()
        new, did = rename_text(cfg, r, text, stats)
        if did:
            changed += 1
            if not check:
                with open(p, 'w', encoding='utf-8', newline='') as f:
                    f.write(new)

    # Expected-count assertions guard against silently losing a protection rule.
    count_changes = []
    for pattern, expected in cfg['expected_counts'].items():
        got = stats.get(pattern, 0)
        if got != expected:
            if not update_counts:
                raise SystemExit(f'expected {expected} protected occurrences of /{pattern}/, found {got}')
            count_changes.append((pattern, expected, got))
            cfg['expected_counts'][pattern] = got
    if count_changes:
        for pattern, expected, got in count_changes:
            print(f'expected_counts /{pattern}/: {expected} -> {got}')
        with open(CONFIG, 'w', encoding='utf-8', newline='\n') as f:
            json.dump(cfg, f, indent=2, ensure_ascii=False)
            f.write('\n')
        print(f'updated {CONFIG}')

    # Rename files and directories bottom-up (paths under path_exclude keep their upstream names).
    moves = 0
    for top in cfg['roots']:
        for dirpath, dirnames, filenames in os.walk(os.path.join(ROOT, top), topdown=False):
            for name in filenames + dirnames:
                src = os.path.join(dirpath, name)
                if matches_any(rel(src), cfg['path_exclude']):
                    continue
                new = rename_path_component(name)
                if new != name:
                    moves += 1
                    if not check:
                        dst = os.path.join(dirpath, new)
                        if os.path.exists(dst):
                            raise SystemExit(f'rename collision: {rel(src)} -> {rel(dst)}')
                        os.rename(src, dst)

    print(f'{"would change" if check else "changed"} {changed} of {len(files)} files, {moves} path renames')
    for k in sorted(stats):
        print(f'  {stats[k]:6d}  {k}')


if __name__ == '__main__':
    main()
