"""diff_manifests.py - compare two manifests made by manifest.py.
Prints: ADDED / REMOVED / CHANGED paths (changed = same path, different SHA-1), then a one-line verdict.
usage: python -I diff_manifests.py <before> <after> [allowed-path ...]
Exit code 0 when every difference is within the allowed paths (default: report.json only), 1 otherwise.
"""
import sys


def load(path):
    d = {}
    with open(path, 'r', encoding='utf-8') as f:
        for line in f:
            line = line.rstrip('\n')
            if not line:
                continue
            digest, rel = line.split('  ', 1)
            d[rel] = digest
    return d


a = load(sys.argv[1])
b = load(sys.argv[2])
allowed = set(sys.argv[3:]) or {'report.json'}
added = sorted(set(b) - set(a))
removed = sorted(set(a) - set(b))
changed = sorted(p for p in set(a) & set(b) if a[p] != b[p])
for tag, items in (('ADDED', added), ('REMOVED', removed), ('CHANGED', changed)):
    print('%s %d' % (tag, len(items)))
    for p in items[:60]:
        print('  ' + p)
    if len(items) > 60:
        print('  ... (%d more)' % (len(items) - 60))
outside = [p for p in added + removed + changed if p not in allowed]
print('files before %d, after %d' % (len(a), len(b)))
print('VERDICT: %s' % ('within the allowed set %s' % sorted(allowed) if not outside else 'OUTSIDE the allowed set: %d paths (STOP)' % len(outside)))
sys.exit(0 if not outside else 1)
