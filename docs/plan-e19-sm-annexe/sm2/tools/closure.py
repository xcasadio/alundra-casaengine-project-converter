"""For each entity record that runs a 0x6D-without-0x6A program: the closure of programs it can switch to (0x40 slot index),
and whether any C program of the closure contains 0x6A (which would leave Parameters[4] set). Read-only. python -B closure.py"""
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import corpus2 as C, cfg2, tilemap2 as T, records as R

SLOTS = 'ABCDEF'
KEYS = R.KEYS


def prog_ops(codes, entry):
    seen, marks, ru, extra = cfg2.reach_walkprogram(codes, entry)
    return seen, extra


def closure(mid, rec_props):
    base, d = C.load(mid)
    codes = d['Codes']
    roots = []
    for s in SLOTS:
        raw = int(rec_props[KEYS[s]])
        idx = raw & 0x7F
        if idx != 0:
            roots.append((s, idx, raw))
    todo = [(s, i) for s, i, raw in roots]
    done = set()
    out = []
    while todo:
        s, i = todo.pop()
        if (s, i) in done:
            continue
        done.add((s, i))
        table = d[C.TABLE_KEYS[s]]
        if i >= len(table) or table[i] == 0:
            out.append((s, i, 'EMPTY', None, set()))
            continue
        seen, extra = prog_ops(codes, table[i])
        ops = set(seen.values())
        out.append((s, i, 'PROG', table[i], ops))
        for e in extra:
            todo.append(e)
    return roots, out


if __name__ == '__main__':
    for mid in (55, 56, 57, 59, 60, 307, 344, 442):
        base, d = C.load(mid)
        for lname, o in T.objects(mid):
            if lname != 'Entities':
                continue
            cp = o['custom_properties']
            raw = int(cp[KEYS['C']])
            if ('C', raw & 0x7F) not in R.by_map[mid]:
                continue
            roots, out = closure(mid, cp)
            has6a = [(s, i) for s, i, k, e, ops in out if k == 'PROG' and s == 'C' and 0x6A in ops]
            has6d = [(s, i) for s, i, k, e, ops in out if k == 'PROG' and s == 'C' and 0x6D in ops]
            sw = [(s, i) for s, i, k, e, ops in out if k == 'PROG' and 0x40 in ops]
            print('map %d rec %s %-30s roots=%s  closure=%s  C-with-6A=%s C-with-6D=%s progs-with-0x40=%s' % (
                mid, cp['Index'], cp.get('EntityName', '')[:28], [(s, i) for s, i, r in roots],
                sorted((s, i) for s, i, *_ in out), has6a, has6d, sw))
