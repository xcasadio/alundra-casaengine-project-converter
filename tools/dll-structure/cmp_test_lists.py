"""R5 (T22): compare two `dotnet test --list-tests` outputs.

  python -B tools/dll-structure/cmp_test_lists.py <baseline list> <new list>

The baseline names are mapped by r5-types.tsv (old full class name -> new) and by r5-nested-types.tsv (D-R-21: the nested class, under its outer
class already renamed), then both lists are compared as multisets of full names (method and case included). Exit 0 = identical, 1 = different.
Read-only.
"""
import collections
import csv
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
import r5_lib as L  # noqa: E402


def load(p):
    return [l.strip() for l in open(p, encoding='utf-8', errors='replace').read().splitlines() if l.startswith('    ')]


def main():
    tbl = L.R5()
    top = {t['old_full']: t['new_full'] for t in tbl.types}
    nested = {}
    for r in csv.DictReader(open(HERE / 'r5-nested-types.tsv', encoding='utf-8', newline=''), delimiter='\t'):
        nested[r['old_full']] = r['new_full']

    def mapped(name):
        best = None
        for old in top:
            if name.startswith(old) and name[len(old):len(old) + 1] in ('.', '+') and (best is None or len(old) > len(best)):
                best = old
        out = top[best] + name[len(best):] if best else name
        for o, n in nested.items():
            if out.startswith(o) and out[len(o):len(o) + 1] == '.':
                out = n + out[len(o):]
                break
        return out

    a, b = load(sys.argv[1]), load(sys.argv[2])
    # the nested map is keyed by the outer class under its NEW name: map the outer first, then the nested part
    am = collections.Counter(mapped(x) for x in a)
    bm = collections.Counter(b)
    print(len(a), 'baseline names,', len(b), 'new names')
    only_a, only_b = am - bm, bm - am
    print('only in the baseline (mapped):', sum(only_a.values()), list(only_a)[:5])
    print('only in the new list:', sum(only_b.values()), list(only_b)[:5])
    ok = not only_a and not only_b
    print('IDENTICAL' if ok else 'DIFFERENT')
    return 0 if ok else 1


if __name__ == '__main__':
    sys.exit(main())
