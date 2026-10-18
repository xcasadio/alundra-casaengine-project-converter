#!/usr/bin/env python3
"""Read-only helper: map a line range of an OLD file (at a given commit) to the HEAD file.
Usage: linemap.py <old_commit> <old_path> <start>[-<end>] [<new_path>]
Normalises both sides by replacing every old type name by its final name (table.json)."""
import json, re, subprocess, sys, difflib

REPO = 'D:/development/repo/alundra-casaengine-project-converter'

def git_show(rev, path):
    r = subprocess.run(['git', '-C', REPO, 'show', f'{rev}:{path}'], capture_output=True)
    if r.returncode != 0:
        return None
    return r.stdout.decode('utf-8', 'replace').replace('\r\n', '\n').split('\n')

def read_head(path):
    try:
        return open(f'{REPO}/{path}', encoding='utf-8-sig', errors='replace').read().replace('\r\n', '\n').split('\n')
    except FileNotFoundError:
        return None

tab = json.load(open(f'{REPO}/tools/dll-structure/table.json'))
ren = {}
files = {}
for t in tab['types']:
    if t['old_name'] != t['final']['name']:
        ren[t['old_name']] = t['final']['name']
    files.setdefault(t['old_file'], set()).add(t['final']['file'])
pat = re.compile(r'\b(' + '|'.join(sorted(map(re.escape, ren), key=len, reverse=True)) + r')\b')

def canon(s):
    s = pat.sub(lambda m: ren[m.group(1)], s)
    s = re.sub(r'\bAlundra\.Scripts\b', 'NS', s)
    s = re.sub(r'\bAlundra\.[A-Z][A-Za-z.]*\b', 'NS', s)
    return s.strip()

def main():
    rev, old_path, rng = sys.argv[1:4]
    new_path = sys.argv[4] if len(sys.argv) > 4 else None
    if new_path is None:
        cands = sorted(files.get(old_path, []))
        if not cands:
            print('no table entry for', old_path); return
        new_path = cands[0]
        if len(cands) > 1:
            print('multiple targets', cands)
    a, _, b = rng.partition('-')
    a = int(a); b = int(b) if b else a
    old = git_show(rev, old_path)
    new = read_head(new_path)
    if old is None or new is None:
        print('missing', old is None, new is None); return
    oc = [canon(x) for x in old]
    nc = [canon(x) for x in new]
    sm = difflib.SequenceMatcher(None, oc, nc, autojunk=False)
    mp = {}
    for tag, i1, i2, j1, j2 in sm.get_opcodes():
        if tag == 'equal':
            for k in range(i2 - i1):
                mp[i1 + k + 1] = j1 + k + 1
    print(f'{old_path}@{rev[:7]} ({len(old)} lines) -> {new_path} ({len(new)} lines)')
    for ln in range(a, b + 1):
        print(ln, '->', mp.get(ln, 'NOT-MATCHED'), '|', (old[ln - 1] if ln - 1 < len(old) else '<EOF>').strip()[:110])

if __name__ == '__main__':
    main()


def build_map(rev, old_path, new_path):
    old = git_show(rev, old_path)
    new = read_head(new_path)
    oc = [canon(x) for x in old]
    nc = [canon(x) for x in new]
    sm = difflib.SequenceMatcher(None, oc, nc, autojunk=False)
    mp = {}
    for tag, i1, i2, j1, j2 in sm.get_opcodes():
        if tag == 'equal':
            for k in range(i2 - i1):
                mp[i1 + k + 1] = j1 + k + 1
    return old, new, mp

_main_target = {}
for t in tab['types']:
    if t['main'] and not t['split'] or (t['main'] and t['old_name'] + '.cs' == t['old_file'].split('/')[-1]):
        _main_target.setdefault(t['old_file'], t['final']['file'])

def batch(rev, items):
    cache = {}
    for old_path, rng in items:
        new_path = _main_target.get(old_path) or sorted(files.get(old_path, ['?']))[0]
        if (old_path, new_path) not in cache:
            cache[(old_path, new_path)] = build_map(rev, old_path, new_path)
        old, new, mp = cache[(old_path, new_path)]
        a, _, b = rng.partition('-')
        a = int(a); b = int(b) if b else a
        mapped = [mp.get(i) for i in range(a, b + 1)]
        ok = [m for m in mapped if m]
        status = 'OK' if len(ok) == len(mapped) else ('PARTIAL %d/%d' % (len(ok), len(mapped)) if ok else 'NOT-MATCHED')
        span = f'{new_path}:{min(ok)}-{max(ok)}' if ok else f'{new_path}:?'
        print(f'{old_path}:{rng} -> {span} [{status}]')
        for i in range(a, min(b, a + 3) + 1):
            print('     old', i, '|', (old[i-1] if i-1 < len(old) else '<EOF>').strip()[:130], '| ->', mp.get(i))
