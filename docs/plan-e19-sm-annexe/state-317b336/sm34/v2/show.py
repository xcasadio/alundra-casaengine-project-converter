#!/usr/bin/env python3
"""Read-only: print old lines (at a commit) and new lines (at HEAD) for citation pairs.
Usage: show.py pairs.txt
Each line of pairs.txt: old_path|old_range|new_path|new_range|label  (ranges a or a-b)"""
import subprocess, sys

REPO = 'D:/development/repo/alundra-casaengine-project-converter'
OLD = '9502f75'

cache = {}
def lines(rev, path):
    k = (rev, path)
    if k not in cache:
        r = subprocess.run(['git', '-C', REPO, 'show', f'{rev}:{path}'], capture_output=True)
        if r.returncode != 0:
            cache[k] = None
        else:
            cache[k] = r.stdout.decode('utf-8', 'replace').replace('\r\n', '\n').split('\n')
    return cache[k]

def rng(s):
    a, _, b = s.partition('-')
    a = int(a); b = int(b) if b else a
    return a, b

def dump(rev, path, r, maxl=12):
    L = lines(rev, path)
    if L is None:
        print(f'   [{rev}] MISSING {path}')
        return
    a, b = rng(r)
    n = b - a + 1
    idx = list(range(a, b + 1))
    if n > maxl:
        idx = list(range(a, a + 5)) + [None] + list(range(b - 4, b + 1))
    for i in idx:
        if i is None:
            print('   ...')
            continue
        t = L[i - 1] if 0 < i <= len(L) else '<EOF>'
        print(f'   [{rev}] {i:5d}: {t.rstrip()[:150]}')

for raw in open(sys.argv[1], encoding='utf-8'):
    raw = raw.rstrip('\n')
    if not raw or raw.startswith('#'):
        continue
    op, orr, np_, nr, label = (raw.split('|') + [''] * 5)[:5]
    print(f'=== {label}')
    if op:
        dump(OLD, op, orr)
    if np_:
        dump('HEAD', np_, nr)
