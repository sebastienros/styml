#!/usr/bin/env python3
# Copyright (c) Sébastien Ros. Licensed under the MIT license.
"""Styml's upstream tooling: which dotnet/runtime commit Styml is in step with, what System.Text.Json changed since,
and a three-way sync by replay. The procedure is in docs/upstream-sync.md. Run through the wrappers next to this file:

  eng/upstream/check                                    the record, its tree list and UPSTREAM.md agree
  eng/upstream/verify-replay <runtime-clone>            import + rename of the recorded commit reproduce the recorded
                                                        renamed tree byte for byte
  eng/upstream/target <runtime-clone>                   the newest ref of the followed line (also --ls-remote FILE)
  eng/upstream/status <runtime-clone> [ref]             Markdown report of upstream changes since the baseline
                                                        (--output FILE, --summary-json FILE)
  eng/upstream/sync <runtime-clone> <ref> [--accept-counts] [--keep-scratch]
  eng/upstream/sync --continue | --abort

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


# --- status ------------------------------------------------------------------------------------------------------------

GROUP_NONE, GROUP_EDITED, GROUP_UNEDITED = 'none', 'edited', 'unedited'


def styml_tree(rev='HEAD'):
    tree = {}
    for line in git('ls-tree', '-r', '-z', rev).split('\0'):
        if not line:
            continue
        meta, path = line.split('\t', 1)
        mode, kind, sha = meta.split()
        if kind == 'blob':
            tree[path] = sha
    return tree


def in_areas(path, areas):
    for a in areas:
        if any(fnmatch.fnmatchcase(path, g) for g in a['paths']):
            return a
    return None


def classify(rec, runtime, base, target):
    """Changed upstream files between base and target, each with its group, its Styml path and a note."""
    pm = PathMap(rec)
    moved, removed, areas = counterparts(rec)
    _, baseline = read_tree_list(os.path.join(REPO, rec['tree']))
    ours = styml_tree()
    srcs = pm.sources()
    numstat = {}
    for line in runtime.git('diff', '--no-renames', '--numstat', base, target, '--', *srcs).stdout.splitlines():
        a, d, p = line.split('\t', 2)
        numstat[p] = (a, d)
    files = []
    manifest_files = {src for kind, src, _ in pm.entries if kind == 'file'}
    for line in runtime.git('diff', '--no-renames', '--name-status', base, target, '--', *srcs).stdout.splitlines():
        status, p = line.split('\t', 1)
        renamed = pm.renamed(p)
        if renamed is None:
            continue   # excluded upstream path
        styml_path = moved.get(renamed, renamed)
        notes = []
        area = in_areas(p, areas)
        if status == 'A':
            if styml_path in ours:
                group = GROUP_EDITED
                notes.append('new upstream file; Styml has a file at that path')
            else:
                group = GROUP_NONE
                notes.append('new upstream file')
        else:
            base_blob = baseline.get(renamed)
            if base_blob is None:
                group = GROUP_NONE
                notes.append('not in the recorded baseline tree')
            elif renamed in removed:
                group = GROUP_NONE
                notes.append(f'Styml removed it: {removed[renamed]}')
            elif styml_path not in ours:
                group = GROUP_NONE
                notes.append('Styml deleted or moved it')
            elif ours[styml_path] == base_blob:
                group = GROUP_UNEDITED
            else:
                group = GROUP_EDITED
            if status == 'D':
                notes.append('deleted upstream' + ('; manifest.txt names it' if p in manifest_files else ''))
        if renamed in moved:
            notes.append(f'Styml keeps it as {moved[renamed]}')
        if area:
            notes.append('code Styml replaced')
        a, d = numstat.get(p, ('-', '-'))
        files.append({'upstream': p, 'status': status, 'renamed': renamed, 'styml': styml_path, 'group': group,
                      'area': area, 'notes': notes, 'added': a, 'deleted': d})
    return files


def patch_ids(runtime, side, rng, srcs):
    """commit -> patch id of its diff limited to the imported paths (git patch-id --stable)."""
    log = runtime.git('log', '-p', '--no-merges', '--format=commit %H', side, rng, '--', *srcs).stdout
    p = subprocess.run(['git', '-C', runtime.path, 'patch-id', '--stable'], input=log, text=True,
                       stdout=subprocess.PIPE, check=True)
    ids = {}
    for line in p.stdout.splitlines():
        pid, sha = line.split()
        ids[sha] = pid
    return ids


def upstream_commits(runtime, base, target, srcs):
    """Commits reachable from target and not from base that touch srcs. A commit whose change to the imported paths
    is patch-equivalent to one on the baseline's side (a backport) is marked '='. Equivalence is computed on the
    imported paths only (path-limited patch ids), which is what matters here and much faster than --cherry-mark."""
    shallow_warning = None
    if runtime.git('merge-base', base, target, check=False).returncode != 0:
        shallow_warning = ('no merge base between the baseline and the target in this clone (shallow fetch?): commits are '
                           'listed as target ^baseline, without detecting backports')
        rng, left = f'{base}..{target}', {}
        side = '--topo-order'
    else:
        rng = f'{base}...{target}'
        left = set(patch_ids(runtime, '--left-only', rng, srcs).values())
        side = '--right-only'
    right = patch_ids(runtime, side, rng, srcs) if left else {}
    out = runtime.git('log', '--no-merges', side, '--format=%x00%H %cs %s', '--name-only', rng, '--', *srcs).stdout
    commits = []
    for chunk in out.split('\0')[1:]:
        lines = chunk.strip('\n').split('\n')
        sha, date, subject = (lines[0].split(' ', 2) + [''])[:3]
        mark = '=' if right.get(sha) in left else '>'
        commits.append({'mark': mark, 'sha': sha, 'date': date, 'subject': subject,
                        'files': [l for l in lines[1:] if l.strip()]})
    return commits, shallow_warning


def code(s):
    """Untrusted text (commit subjects, paths) as a Markdown code span: no mention, link or reference fires."""
    s = s.replace('`', "'").replace('\n', ' ').strip()
    return f'`{s}`' if s else '``'


def commit_link(repo_url, sha):
    return f'[`{sha[:12]}`]({repo_url}/commit/{sha})'


def cmd_status(args):
    if not args:
        die('usage: eng/upstream/status <runtime-clone> [ref] [--output FILE] [--summary-json FILE]')
    opts = {}
    for opt in ('--output', '--summary-json'):
        if opt in args:
            i = args.index(opt)
            opts[opt] = args[i + 1]
            args = args[:i] + args[i + 2:]
    output = opts.get('--output')
    rec = record()
    runtime = Runtime(args[0])
    target_ref = args[1] if len(args) > 1 else None
    if target_ref is None:
        follow = rec['follow']
        target_ref = follow['ref'] if follow['line'] == 'main' else (
            pick_tag(follow['tags'], runtime.git('tag', '--list').stdout.split()) or rec['ref'])
    base = rec['commit']
    if runtime.git('cat-file', '-e', f'{base}^{{commit}}', check=False).returncode != 0:
        die(f'the baseline commit {base} is not in {runtime.path} (fetch it first)')
    target = runtime.rev(target_ref)
    files = classify(rec, runtime, base, target)
    pm = PathMap(rec)
    commits, warning = upstream_commits(runtime, base, target, pm.sources())
    text = render_status(rec, runtime, target_ref, target, files, commits, warning)
    if '--summary-json' in opts:
        groups = {g: sum(1 for f in files if f['group'] == g) for g in (GROUP_NONE, GROUP_EDITED, GROUP_UNEDITED)}
        write_json(opts['--summary-json'], {
            'baseline_ref': rec['ref'], 'baseline_commit': rec['commit'], 'target_ref': target_ref,
            'target_commit': target, 'files': len(files), 'groups': groups,
            'commits': sum(1 for c in commits if c['mark'] != '='),
            'backports': sum(1 for c in commits if c['mark'] == '=')})
    if output:
        with open(output, 'w', encoding='utf-8', newline='\n') as f:
            f.write(text)
    else:
        sys.stdout.write(text)


def render_status(rec, runtime, target_ref, target, files, commits, warning):
    url = rec['repository'].rstrip('/')
    by_path = {f['upstream']: f for f in files}
    real = [c for c in commits if c['mark'] != '=']
    backports = [c for c in commits if c['mark'] == '=']
    for c in real:
        c['changed'] = [p for p in c['files'] if p in by_path]
    out = []
    w = out.append
    w(f'# Upstream System.Text.Json changes: {code(rec["ref"])} → {code(target_ref)}\n')
    w('| | |\n|---|---|')
    w(f'| Baseline | {code(rec["ref"])}, {commit_link(url, rec["commit"])}, synced {rec["synced"]} |')
    w(f'| Target | {code(target_ref)}, {commit_link(url, target)}, {runtime.date(target)} |')
    follow = rec['follow']
    w(f'| Followed line | {follow["line"]} ({code(follow.get("tags") or follow.get("ref"))}) |')
    w(f'| Upstream commits | {len(real)} touching the imported paths'
      + (f', and {len(backports)} already in the baseline as backports' if backports else '') + ' |')
    w('')
    if warning:
        w(f'> **Note:** {warning}.\n')
    if target == rec['commit'] or not files:
        w(f'Styml is in step with {code(target_ref)}: no imported upstream file changed since the baseline.\n')
        return '\n'.join(out) + '\n'

    groups = [
        (GROUP_NONE, 'No counterpart in Styml', 'a new upstream file (the sync adds it), or a file Styml deleted or '
         'folded into its own (the sync leaves it out); either way a person decides what Styml needs'),
        (GROUP_EDITED, 'Edited by Styml', 'the sync merges these three-way and may leave conflicts'),
        (GROUP_UNEDITED, 'Not edited by Styml', 'the sync applies these mechanically'),
    ]

    def lines_of(fs):
        a = sum(int(f['added']) for f in fs if f['added'].isdigit())
        d = sum(int(f['deleted']) for f in fs if f['deleted'].isdigit())
        return f'+{a} / −{d}'

    def commits_of(fs):
        paths = {f['upstream'] for f in fs}
        return [c for c in real if paths & set(c['changed'])]

    w('| Group | Files | Lines | Commits |\n|---|---:|---:|---:|')
    for key, title, _ in groups:
        fs = [f for f in files if f['group'] == key]
        w(f'| {title} | {len(fs)} | {lines_of(fs)} | {len(commits_of(fs))} |')
    w(f'| **Total** | **{len(files)}** | **{lines_of(files)}** | **{len(real)}** |')
    w('')

    review = [f for f in files if f['group'] == GROUP_NONE or f['area']]
    w('## For a person to judge\n')
    w('Upstream commits that change files with no counterpart in Styml, or code Styml replaced with YAML code (the '
      'reader, document parsing, the writer: eng/upstream/counterparts.json). A fix there may need an equivalent in '
      'the YAML reader or writer even when the merge is clean or does not apply.\n')
    rc = commits_of(review)
    if not rc:
        w('None.\n')
    for c in rc:
        touched = [by_path[p] for p in c['changed'] if by_path[p] in review]
        names = ', '.join(code(os.path.basename(f['upstream'])) for f in touched[:6])
        more = f' and {len(touched) - 6} more' if len(touched) > 6 else ''
        w(f'- {commit_link(url, c["sha"])} {c["date"]} {code(c["subject"])}: {names}{more}')
    w('')

    for key, title, blurb in groups:
        fs = sorted((f for f in files if f['group'] == key), key=lambda f: f['upstream'])
        w(f'## {title} ({len(fs)} files)\n')
        w(f'{blurb[0].upper() + blurb[1:]}.\n')
        if not fs:
            w('None.\n')
            continue
        w('| Upstream file | Styml file | Change | Lines | Commits | Notes |\n|---|---|---|---:|---:|---|')
        prefix = 'src/libraries/'
        for f in fs:
            up = f['upstream'][len(prefix):] if f['upstream'].startswith(prefix) else f['upstream']
            n = sum(1 for c in real if f['upstream'] in c['changed'])
            change = {'A': 'added', 'M': 'modified', 'D': 'deleted'}.get(f['status'], f['status'])
            w(f'| {code(up)} | {code(f["styml"])} | {change} | +{f["added"]} / −{f["deleted"]} | {n} | '
              f'{"; ".join(f["notes"])} |')
        w('')
        gc = commits_of(fs)
        if gc and key != GROUP_NONE:
            w('<details><summary>Commits</summary>\n')
            for c in gc:
                w(f'- {commit_link(url, c["sha"])} {c["date"]} {code(c["subject"])}')
            w('\n</details>\n')

    if backports:
        w(f'## Already in the baseline ({len(backports)} commits)\n')
        w('Patch-equivalent to a commit the baseline already contains (backports); nothing to bring in.\n')
        for c in backports:
            w(f'- {commit_link(url, c["sha"])} {c["date"]} {code(c["subject"])}')
        w('')
    w(f'Produced by `eng/upstream/status`. Upstream paths are relative to {code("src/libraries/")} when under it; '
      f'the imported paths are {code(rec["manifest"])} minus {code(rec["exclude"])}.')
    return '\n'.join(out) + '\n'


# --- sync --------------------------------------------------------------------------------------------------------------

def state_dir():
    return os.path.join(REPO, git('rev-parse', '--git-path', 'styml-upstream-sync').strip())


def is_binary(path):
    with open(path, 'rb') as f:
        return b'\0' in f.read(8000)


def cmd_sync(args):
    if '--continue' in args:
        return sync_continue()
    if '--abort' in args:
        return sync_abort()
    flags = {a for a in args if a.startswith('--')}
    pos = [a for a in args if not a.startswith('--')]
    if len(pos) != 2:
        die('usage: eng/upstream/sync <runtime-clone> <ref> [--accept-counts] [--keep-scratch] | --continue | --abort')
    unknown = flags - {'--accept-counts', '--keep-scratch'}
    if unknown:
        die(f'unknown option {sorted(unknown)[0]}')
    sd = state_dir()
    if os.path.exists(sd):
        die(f'a sync is in progress ({sd}): finish it with eng/upstream/sync --continue or --abort')
    if git('status', '--porcelain').strip():
        die('the working tree has changes: commit or set them aside before a sync')
    rec = record()
    runtime = Runtime(pos[0])
    target_ref = pos[1]
    base = runtime.rev(rec['commit'])
    target = runtime.rev(target_ref)
    if target == base:
        print(f'{target_ref} is the baseline commit: nothing to sync')
        return
    scratch = tempfile.mkdtemp(prefix='styml-sync-')
    print(f'scratch: {scratch}')

    # 1. The old baseline, replayed with today's tooling, must be exactly the recorded renamed tree.
    print(f'replaying the baseline {rec["ref"]} ({base[:12]}) ...')
    base_tree, _ = replay(runtime, base, os.path.join(scratch, 'base'))
    _, recorded = read_tree_list(os.path.join(REPO, rec['tree']))
    diffs = compare_trees(recorded, base_tree)
    if diffs:
        print('\n'.join(diffs[:40]))
        die(f'the replay of the baseline differs from {rec["tree"]} in {len(diffs)} files: the import or rename tooling '
            f'changed since the last sync. Fix the tooling first (eng/upstream/verify-replay). Scratch: {scratch}')

    # 2. The target, with the count guards recording instead of failing.
    print(f'replaying the target {target_ref} ({target[:12]}) ...')
    target_tree, missing = replay(runtime, target, os.path.join(scratch, 'target'), update_counts=True, allow_missing=True)
    count_files = ['eng/rename/protect.json', 'eng/rename/passb/passb.json']
    changed_counts = []
    for cf in count_files:
        with open(os.path.join(REPO, cf), encoding='utf-8') as a, \
                open(os.path.join(scratch, 'target', cf), encoding='utf-8') as b:
            if a.read() != b.read():
                changed_counts.append(cf)
    if changed_counts:
        for cf in changed_counts:
            with open(os.path.join(REPO, cf), encoding='utf-8') as a, \
                    open(os.path.join(scratch, 'target', cf), encoding='utf-8') as b:
                sys.stdout.writelines(difflib.unified_diff(a.readlines(), b.readlines(), f'{cf} (baseline)',
                                                           f'{cf} (target)', n=1))
        if '--accept-counts' not in flags:
            die('the rename guards count differently at the target (above). Check that each new occurrence is right '
                '(a protected token that must stay Json, a literal that names a renamed identifier), then rerun with '
                f'--accept-counts. Nothing was changed in this repository. Scratch kept: {scratch}')

    # 3. Three-way merge of base -> target onto Styml, file by file.
    moved, removed, areas = counterparts(rec)
    ours_head = styml_tree()
    result = {'merged': [], 'applied': [], 'added': [], 'deleted': [], 'conflicts': [], 'modify_delete': [],
              'no_counterpart': [], 'binary_conflicts': []}
    touched = set()
    for p in sorted(set(base_tree) | set(target_tree)):
        b, t = base_tree.get(p), target_tree.get(p)
        if b == t:
            continue
        ours_path = moved.get(p, p)
        ours_file = os.path.join(REPO, ours_path)
        ours = ours_head.get(ours_path)
        base_file = os.path.join(scratch, 'base', p)
        target_file = os.path.join(scratch, 'target', p)
        if p in removed:
            result['no_counterpart'].append(f'{p} ({removed[p]})')
            continue
        if b is not None and ours is None:
            result['no_counterpart'].append(f'{p} (Styml deleted or moved it)')
            continue
        if b is None and ours is None:
            os.makedirs(os.path.dirname(ours_file), exist_ok=True)
            shutil.copyfile(target_file, ours_file)
            result['added'].append(ours_path)
            touched.add(ours_path)
            continue
        if t is None:
            if ours == b:
                os.remove(ours_file)
                result['deleted'].append(ours_path)
                touched.add(ours_path)
            else:
                result['modify_delete'].append(ours_path)
            continue
        if ours == b:
            shutil.copyfile(target_file, ours_file)
            result['applied'].append(ours_path)
            touched.add(ours_path)
            continue
        if ours == t:
            continue
        if b is None:
            base_file = os.path.join(scratch, 'empty')
            open(base_file, 'w').close()
        if is_binary(ours_file) or is_binary(target_file) or is_binary(base_file):
            result['binary_conflicts'].append(ours_path)
            continue
        p_merge = run(['git', 'merge-file', '-L', 'Styml', '-L', f'upstream {rec["ref"]}', '-L', f'upstream {target_ref}',
                       ours_file, base_file, target_file], check=False)
        if p_merge.returncode < 0 or p_merge.returncode > 127:
            die(f'git merge-file failed on {ours_path}: {p_merge.stderr}')
        touched.add(ours_path)
        (result['conflicts'] if p_merge.returncode > 0 else result['merged']).append(ours_path)
    for cf in changed_counts:
        shutil.copyfile(os.path.join(scratch, 'target', cf), os.path.join(REPO, cf))
        touched.add(cf)

    os.makedirs(sd)
    write_tree_list(os.path.join(sd, 'target-tree.txt'), target, target_tree)
    write_json(os.path.join(sd, 'state.json'), {
        'from_ref': rec['ref'], 'from_commit': base, 'ref': target_ref, 'commit': target,
        'touched': sorted(touched), 'result': result, 'missing_at_target': missing, 'counts': changed_counts,
        'scratch': scratch if '--keep-scratch' in flags else None})
    if '--keep-scratch' not in flags:
        shutil.rmtree(scratch, ignore_errors=True)
    print_sync_summary(rec['ref'], target_ref, result, missing, changed_counts)


def print_sync_summary(from_ref, to_ref, result, missing, changed_counts):
    print(f'\nsync {from_ref} -> {to_ref}:')
    labels = [('applied', 'applied (Styml had not edited them)'), ('merged', 'merged cleanly'),
              ('added', 'added (new upstream files)'), ('deleted', 'deleted (deleted upstream, unedited here)'),
              ('conflicts', 'CONFLICTS (markers left in the file)'),
              ('modify_delete', 'DELETED UPSTREAM, EDITED HERE (kept; decide by hand)'),
              ('binary_conflicts', 'BINARY CONFLICTS (kept as Styml has them; decide by hand)'),
              ('no_counterpart', 'no counterpart in Styml (not merged; judge by hand)')]
    for key, label in labels:
        items = result[key]
        print(f'  {len(items):4d}  {label}')
        if key in ('conflicts', 'modify_delete', 'binary_conflicts', 'no_counterpart'):
            for i in items:
                print(f'          {i}')
    for m in missing:
        print(f'  manifest entry not at the target (remove it from eng/import/manifest.txt or exclude.txt): {m}')
    for cf in changed_counts:
        print(f'  rename counts updated: {cf}')
    print('\nResolve, build and test (docs/upstream-sync.md), then: eng/upstream/sync --continue   (or --abort)')


CONFLICT_RE = re.compile(r'^(<{7}|>{7}) ', re.M)


def sync_continue():
    sd = state_dir()
    if not os.path.exists(os.path.join(sd, 'state.json')):
        die('no sync in progress')
    st = load_json(os.path.join(sd, 'state.json'))
    left = []
    for p in st['touched']:
        f = os.path.join(REPO, p)
        if os.path.isfile(f) and not is_binary(f):
            with open(f, encoding='utf-8', errors='replace') as fh:
                if CONFLICT_RE.search(fh.read()):
                    left.append(p)
    if left:
        die('conflict markers remain in:\n  ' + '\n  '.join(left))
    rec = record()
    # The next replay of the new baseline is strict: manifest entries absent at the target must be gone.
    entries, excludes = manifest_entries(rec)
    listed = {src for _, src, _ in entries} | {f'exclude.txt: {e}' for e in excludes}
    stale = [m for m in st['missing_at_target'] if m in listed]
    if stale:
        die('remove these entries, absent at the target, from eng/import/manifest.txt or exclude.txt (and the project '
            'items that link them):\n  ' + '\n  '.join(stale))
    if rec['commit'] != st['from_commit']:
        die(f'baseline.json changed during the sync ({rec["commit"]}, expected {st["from_commit"]})')
    today = datetime.now(timezone.utc).strftime('%Y-%m-%d')
    rec['ref'], rec['commit'], rec['synced'] = st['ref'], st['commit'], today
    write_json(RECORD, rec)
    target_commit, target_tree = read_tree_list(os.path.join(sd, 'target-tree.txt'))
    write_tree_list(os.path.join(REPO, rec['tree']), target_commit, target_tree)
    with open(UPSTREAM_MD, encoding='utf-8') as f:
        text = f.read()
    text = re.sub(r'^\| Ref \| .*? \|\s*$', f'| Ref | `{st["ref"]}` |', text, count=1, flags=re.M)
    text = re.sub(r'^\| Commit \| .*? \|\s*$', f'| Commit | `{st["commit"]}` |', text, count=1, flags=re.M)
    text = re.sub(r'^\| Synced \| .*? \|\s*$', f'| Synced | {today} |', text, count=1, flags=re.M)
    r = st['result']
    n_conf = len(r['conflicts']) + len(r['modify_delete']) + len(r['binary_conflicts'])
    line = (f'- {today} · `{st["ref"]}` · `{st["commit"]}` · sync from `{st["from_ref"]}`: '
            f'{len(r["applied"]) + len(r["merged"])} files merged, {len(r["added"])} added, {len(r["deleted"])} deleted, '
            f'{n_conf} resolved by hand')
    m = re.search(r'^## Syncs\n(.*?)(?=^## |\Z)', text, re.M | re.S)
    if not m:
        die('UPSTREAM.md has no "## Syncs" section')
    body = m.group(1)
    last = [mm for mm in re.finditer(r'^- .*$', body, re.M)][-1]
    insert_at = m.start(1) + last.end()
    text = text[:insert_at] + '\n' + line + text[insert_at:]
    with open(UPSTREAM_MD, 'w', encoding='utf-8', newline='\n') as f:
        f.write(text)
    cmd_check([])
    git('add', '-A')
    msg = (f'Sync System.Text.Json with dotnet/runtime {st["ref"]}\n\n'
           f'Three-way merge by replay of {st["from_ref"]} ({st["from_commit"][:12]}) to {st["ref"]} '
           f'({st["commit"][:12]}):\n'
           f'{len(r["applied"])} files applied, {len(r["merged"])} merged, {len(r["added"])} added, '
           f'{len(r["deleted"])} deleted, {n_conf} resolved by hand,\n'
           f'{len(r["no_counterpart"])} without a counterpart left for review. The record\n'
           f'(eng/upstream/baseline.json, baseline-tree.txt) and UPSTREAM.md follow.\n')
    p = subprocess.run(['git', '-C', REPO, 'commit', '-q', '-F', '-'], input=msg, text=True)
    if p.returncode != 0:
        die('git commit failed; the record is updated in the working tree, the sync state is kept')
    shutil.rmtree(sd, ignore_errors=True)
    print(git('log', '-1', '--format=%h %s').strip())


def sync_abort():
    sd = state_dir()
    if not os.path.exists(os.path.join(sd, 'state.json')):
        die('no sync in progress')
    st = load_json(os.path.join(sd, 'state.json'))
    head = styml_tree()
    at_head = [p for p in st['touched'] if p in head]
    if at_head:
        git('checkout', 'HEAD', '--', *at_head)
    for p in st['touched']:
        if p not in head and os.path.isfile(os.path.join(REPO, p)):
            os.remove(os.path.join(REPO, p))
    shutil.rmtree(sd, ignore_errors=True)
    print(f'sync aborted: {len(at_head)} files restored, {len(st["touched"]) - len(at_head)} added files removed')


COMMANDS = {'check': cmd_check, 'verify-replay': cmd_verify_replay, 'target': cmd_target, 'status': cmd_status,
            'sync': cmd_sync}

if __name__ == '__main__':
    if len(sys.argv) < 2 or sys.argv[1] not in COMMANDS:
        die(__doc__)
    COMMANDS[sys.argv[1]](sys.argv[2:])
