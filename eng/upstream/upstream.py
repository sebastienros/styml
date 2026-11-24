#!/usr/bin/env python3
# Copyright (c) Sébastien Ros. Licensed under the MIT license.
"""Styml's upstream tooling: which dotnet/runtime commit Styml is in step with, what System.Text.Json changed since,
and a three-way sync by replay. The procedure is in docs/upstream-sync.md. Run through the wrappers next to this file:

  eng/upstream/check                                    the record, its tree list and UPSTREAM.md agree
  eng/upstream/verify-replay <runtime-clone>            import + rename of the recorded commit reproduce the recorded
                                                        renamed tree byte for byte
  eng/upstream/target <runtime-clone>                   the newest ref of the followed line (also --ls-remote FILE)

The runtime clone is only read (rev-parse, log, diff, ls-tree, archive, cat-file); nothing is written to it. Replays
run in a scratch directory. Only 'sync' writes to this repository, and only 'sync --continue' commits.
"""
import difflib
import fnmatch
import hashlib
import importlib.util
import json
import os
import re
import shutil
import subprocess
import sys
import tempfile
from datetime import datetime, timezone

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, '..', '..'))
RECORD = os.path.join(HERE, 'baseline.json')
UPSTREAM_MD = os.path.join(REPO, 'UPSTREAM.md')
SHA_RE = re.compile(r'^[0-9a-f]{40}$')
SYNC_LINE_RE = re.compile(r'^- (\d{4}-\d{2}-\d{2}) · `([^`]+)` · `([0-9a-f]{40})`')
# Paths of a replayed tree that are tooling, not the import.
TOOLING = ('eng/',)


def die(msg, code=1):
    print(msg, file=sys.stderr)
    sys.exit(code)


def run(args, cwd=None, capture=True, check=True, env=None):
    p = subprocess.run(args, cwd=cwd, env=env, text=True,
                       stdout=subprocess.PIPE if capture else None,
                       stderr=subprocess.PIPE if capture else None)
    if check and p.returncode != 0:
        out = ((p.stdout or '') + (p.stderr or '')).strip()
        die(f'command failed ({p.returncode}): {" ".join(args)}\n{out}')
    return p


def git(*args, repo=REPO, check=True):
    return run(['git', '-C', repo, *args], check=check).stdout


# --- the record -----------------------------------------------------------------------------------------------------

def load_json(path):
    with open(path, encoding='utf-8') as f:
        return json.load(f)


def write_json(path, data):
    with open(path, 'w', encoding='utf-8', newline='\n') as f:
        json.dump(data, f, indent=2, ensure_ascii=False)
        f.write('\n')


def record():
    return load_json(RECORD)


def counterparts(rec):
    c = load_json(os.path.join(REPO, rec['counterparts']))
    moved = {k: v for k, v in c['moved'].items() if k != 'comment'}
    removed = {k: v for k, v in c['removed'].items() if k != 'comment'}
    return moved, removed, c['replaced_areas']


def read_tree_list(path):
    commit, tree = None, {}
    with open(path, encoding='utf-8') as f:
        for line in f:
            line = line.rstrip('\n')
            if line.startswith('# commit '):
                commit = line[len('# commit '):].strip()
            elif line and not line.startswith('#'):
                sha, p = line.split(' ', 1)
                tree[p] = sha
    return commit, tree


def write_tree_list(path, commit, tree):
    with open(path, 'w', encoding='utf-8', newline='\n') as f:
        f.write("# The renamed System.Text.Json tree at the commit in eng/upstream/baseline.json: what eng/import and eng/rename\n"
                "# produce from it, one '<git blob SHA-1> <path>' line per file. Written by eng/upstream/sync; checked by\n"
                "# eng/upstream/verify-replay. Do not edit by hand.\n")
        f.write(f'# commit {commit}\n')
        for p in sorted(tree, key=lambda s: s.encode('utf-8')):
            f.write(f'{tree[p]} {p}\n')


def upstream_md_state(text):
    """The opening table's Ref / Commit / Synced rows and the last line of the Syncs section."""
    rows = {}
    for m in re.finditer(r'^\| (Ref|Commit|Synced|Record) \| (.*?) \|\s*$', text, re.M):
        rows.setdefault(m.group(1), m.group(2).strip().strip('`'))
    syncs = None
    m = re.search(r'^## Syncs\n(.*?)(?=^## |\Z)', text, re.M | re.S)
    if m:
        lines = [l for l in m.group(1).splitlines() if l.startswith('- ')]
        syncs = lines
    return rows, syncs


def cmd_check(_args):
    rec = record()
    problems = []
    for key in ('repository', 'follow', 'ref', 'commit', 'synced', 'manifest', 'exclude', 'tree', 'counterparts'):
        if key not in rec:
            problems.append(f'baseline.json: missing "{key}"')
    if problems:
        die('\n'.join(problems))
    if not SHA_RE.match(rec['commit']):
        problems.append(f'baseline.json: commit is not a full SHA-1: {rec["commit"]}')
    if not re.match(r'^\d{4}-\d{2}-\d{2}$', rec['synced']):
        problems.append(f'baseline.json: synced is not a YYYY-MM-DD date: {rec["synced"]}')
    follow = rec['follow']
    if follow.get('line') not in ('servicing', 'main', 'major'):
        problems.append('baseline.json: follow.line must be servicing, main or major')
    elif follow['line'] == 'main' and not follow.get('ref'):
        problems.append('baseline.json: follow.line "main" needs follow.ref')
    elif follow['line'] != 'main':
        try:
            re.compile(follow.get('tags', ''))
        except re.error as e:
            problems.append(f'baseline.json: follow.tags is not a regular expression: {e}')
        if not follow.get('tags'):
            problems.append(f'baseline.json: follow.line "{follow["line"]}" needs follow.tags')
    for key in ('manifest', 'exclude', 'tree', 'counterparts'):
        if not os.path.isfile(os.path.join(REPO, rec[key])):
            problems.append(f'baseline.json: {key} file not found: {rec[key]}')
    if not problems:
        tree_commit, tree = read_tree_list(os.path.join(REPO, rec['tree']))
        if tree_commit != rec['commit']:
            problems.append(f'{rec["tree"]}: lists the tree of {tree_commit}, the record says {rec["commit"]}')
        moved, removed, _ = counterparts(rec)
        for p in list(moved) + list(removed):
            if p not in tree:
                problems.append(f'{rec["counterparts"]}: {p} is not a path of the renamed baseline')
        for p, dst in moved.items():
            if not os.path.isfile(os.path.join(REPO, dst)):
                problems.append(f'{rec["counterparts"]}: moved target {dst} does not exist')

    with open(UPSTREAM_MD, encoding='utf-8') as f:
        rows, syncs = upstream_md_state(f.read())
    for row, key in (('Ref', 'ref'), ('Commit', 'commit'), ('Synced', 'synced')):
        if rows.get(row) != rec[key]:
            problems.append(f'UPSTREAM.md: the "{row}" row says {rows.get(row)!r}, baseline.json says {rec[key]!r}')
    if 'eng/upstream/baseline.json' not in (rows.get('Record') or ''):
        problems.append('UPSTREAM.md: the opening table has no "Record" row naming eng/upstream/baseline.json')
    if not syncs:
        problems.append('UPSTREAM.md: no "## Syncs" section with "- YYYY-MM-DD · `ref` · `commit` · ..." lines')
    else:
        m = SYNC_LINE_RE.match(syncs[-1])
        if not m:
            problems.append(f'UPSTREAM.md: the last Syncs line is not "- YYYY-MM-DD · `ref` · `commit` · ...": {syncs[-1]}')
        elif (m.group(1), m.group(2), m.group(3)) != (rec['synced'], rec['ref'], rec['commit']):
            problems.append(f'UPSTREAM.md: the last Syncs line is {m.group(1)} {m.group(2)} {m.group(3)}, '
                            f'baseline.json says {rec["synced"]} {rec["ref"]} {rec["commit"]}')
        for line in syncs:
            if not SYNC_LINE_RE.match(line):
                problems.append(f'UPSTREAM.md: malformed Syncs line: {line}')
    if problems:
        die('\n'.join(problems))
    print(f'upstream record consistent: {rec["ref"]} {rec["commit"]} synced {rec["synced"]}')


# --- the runtime clone (read only) ----------------------------------------------------------------------------------

class Runtime:
    def __init__(self, path):
        if not os.path.isdir(path):
            die(f'not a directory: {path}')
        self.path = os.path.abspath(path)

    def git(self, *args, check=True):
        return run(['git', '-C', self.path, *args], check=check)

    def rev(self, ref):
        p = self.git('rev-parse', '--verify', '--quiet', f'{ref}^{{commit}}', check=False)
        if p.returncode != 0:
            die(f'{ref}: not a commit in {self.path}')
        return p.stdout.strip()

    def date(self, commit):
        return self.git('log', '-1', '--format=%cs', commit).stdout.strip()


def manifest_entries(rec, root=REPO):
    entries = []
    with open(os.path.join(root, rec['manifest']), encoding='utf-8') as f:
        for line in f:
            parts = line.split()
            if not parts or parts[0].startswith('#'):
                continue
            entries.append((parts[0], parts[1], parts[2]))
    excludes = []
    with open(os.path.join(root, rec['exclude']), encoding='utf-8') as f:
        for line in f:
            line = line.strip()
            if line and not line.startswith('#'):
                excludes.append(line)
    return entries, excludes


def load_rename_module():
    sys.dont_write_bytecode = True   # no __pycache__ in eng/rename
    spec = importlib.util.spec_from_file_location('styml_rename', os.path.join(REPO, 'eng', 'rename', 'rename.py'))
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


class PathMap:
    """Upstream path -> path in the renamed baseline, the way eng/import and eng/rename place it."""

    def __init__(self, rec):
        self.entries, self.excludes = manifest_entries(rec)
        self.rename = load_rename_module()
        self.cfg = self.rename.load_config()

    def excluded(self, p):
        return any(p == e or p.startswith(e + '/') for e in self.excludes)

    def imported(self, p):
        """Import-layout path for an upstream path, or None when the manifest does not import it."""
        if self.excluded(p):
            return None
        for kind, src, dst in self.entries:
            if kind == 'file' and p == src:
                return dst
            if kind == 'dir' and p.startswith(src + '/'):
                return dst + p[len(src):]
        return None

    def renamed(self, p):
        dst = self.imported(p)
        if dst is None:
            return None
        parts = dst.split('/')
        if parts[0] not in self.cfg['roots']:
            return dst
        out = []
        for i in range(len(parts)):
            # rename.py renames bottom-up and tests each entry's original path against path_exclude.
            original = '/'.join(parts[:i + 1])
            if self.rename.matches_any(original, self.cfg['path_exclude']):
                out.append(parts[i])
            else:
                out.append(self.rename.rename_path_component(parts[i]))
        return '/'.join(out)

    def sources(self):
        return [src for _, src, _ in self.entries]


# --- following the upstream line -------------------------------------------------------------------------------------

def version_key(tag):
    nums = re.findall(r'\d+', tag)
    return [int(n) for n in nums]


def pick_tag(pattern, tags):
    rx = re.compile(pattern)
    matching = [t for t in tags if rx.search(t)]
    return max(matching, key=version_key) if matching else None


def cmd_target(args):
    """Prints the ref the record follows: the newest matching tag, or the followed branch."""
    rec = record()
    follow = rec['follow']
    if follow['line'] == 'main':
        print(follow['ref'])
        return
    if args and args[0] == '--ls-remote':
        with open(args[1], encoding='utf-8') as f:
            tags = [l.split('\t', 1)[1].strip().removeprefix('refs/tags/') for l in f if '\t' in l]
    else:
        if not args:
            die('usage: eng/upstream/target <runtime-clone> | --ls-remote <file>')
        tags = Runtime(args[0]).git('tag', '--list').stdout.split()
    tag = pick_tag(follow['tags'], tags)
    print(tag or rec['ref'])


# --- hashing and replay -----------------------------------------------------------------------------------------------

def blob_sha(path):
    with open(path, 'rb') as f:
        data = f.read()
    return hashlib.sha1(b'blob %d\0' % len(data) + data).hexdigest()


def tree_of(root):
    tree = {}
    for dirpath, dirnames, filenames in os.walk(root):
        dirnames.sort()
        for name in filenames:
            full = os.path.join(dirpath, name)
            rel = os.path.relpath(full, root).replace(os.sep, '/')
            if rel.startswith(TOOLING):
                continue
            tree[rel] = blob_sha(full)
    return tree


def replay(runtime, commit, dest, update_counts=False, allow_missing=False):
    """Import and rename one upstream commit into dest, with this repository's eng/import and eng/rename."""
    os.makedirs(os.path.join(dest, 'eng'))
    shutil.copytree(os.path.join(REPO, 'eng', 'import'), os.path.join(dest, 'eng', 'import'))
    shutil.copytree(os.path.join(REPO, 'eng', 'rename'), os.path.join(dest, 'eng', 'rename'),
                    ignore=shutil.ignore_patterns('bin', 'obj', '__pycache__'))
    log = []

    def step(name, args, cwd=None):
        p = run(args, cwd=cwd, check=False)
        out = ((p.stdout or '') + (p.stderr or '')).strip()
        log.append(f'## {name}\n{out}')
        if p.returncode != 0:
            die(f'{name} failed for {commit} in {dest}:\n{out}')
        return out

    imp = ['bash', os.path.join(dest, 'eng', 'import', 'import.sh'), runtime.path, commit]
    out = step('import', imp + (['--allow-missing'] if allow_missing else []))
    missing = [l.split(': ', 1)[1] for l in out.splitlines() if l.startswith('not at ')]
    step('verify', ['bash', os.path.join(dest, 'eng', 'import', 'verify.sh'), runtime.path, commit])
    flag = ['--update-counts'] if update_counts else []
    step('rename pass A', [sys.executable, '-I', os.path.join(dest, 'eng', 'rename', 'rename.py'), *flag])
    step('rename pass B', ['dotnet', 'run', os.path.join(REPO, 'eng', 'rename', 'passb', 'passb.cs'), '--', *flag], cwd=dest)
    with open(os.path.join(dest, 'eng', 'replay.log'), 'w', encoding='utf-8') as f:
        f.write('\n\n'.join(log) + '\n')
    return tree_of(dest), missing


def compare_trees(expected, actual):
    diffs = []
    for p in sorted(set(expected) | set(actual)):
        if p not in actual:
            diffs.append(f'MISSING  {p}')
        elif p not in expected:
            diffs.append(f'EXTRA    {p}')
        elif expected[p] != actual[p]:
            diffs.append(f'DIFFERS  {p}')
    return diffs


def cmd_verify_replay(args):
    if not args:
        die('usage: eng/upstream/verify-replay <runtime-clone> [--keep-scratch]')
    rec = record()
    runtime = Runtime(args[0])
    if runtime.rev(rec['commit']) != rec['commit']:
        die(f'{rec["commit"]} does not resolve to itself in {runtime.path}')
    tree_commit, expected = read_tree_list(os.path.join(REPO, rec['tree']))
    if tree_commit != rec['commit']:
        die(f'{rec["tree"]} lists {tree_commit}, the record says {rec["commit"]}')
    scratch = tempfile.mkdtemp(prefix='styml-replay-')
    actual, _ = replay(runtime, rec['commit'], os.path.join(scratch, 'tree'))
    diffs = compare_trees(expected, actual)
    for d in diffs:
        print(d)
    print(f'replayed {rec["ref"]} ({rec["commit"]}): {len(actual)} files, {len(diffs)} differences from {rec["tree"]}')
    if diffs or '--keep-scratch' in args:
        print(f'scratch tree kept: {scratch}')
    else:
        shutil.rmtree(scratch, ignore_errors=True)
    sys.exit(1 if diffs else 0)


COMMANDS = {'check': cmd_check, 'verify-replay': cmd_verify_replay, 'target': cmd_target}

if __name__ == '__main__':
    if len(sys.argv) < 2 or sys.argv[1] not in COMMANDS:
        die(__doc__)
    COMMANDS[sys.argv[1]](sys.argv[2:])
