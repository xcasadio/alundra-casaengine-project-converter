"""Critic's own memo dataflow over the 129 sites' programs (independent of cfg2.py / memo_static.py).
Writers: the binary's (my own scan: 0B 1C 1D 1E 1F 20-23 37 5F BB ...). Output: for each read (6B/6C/6D) the set of states {I,S,C} on its axis."""
import sys, os, collections, json
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from dllmodel import load_codes, SIZES, sext16
from mycorpus import walk

WR = {
    0: {0x0B, 0x1C, 0x1D, 0x1E, 0x1F, 0x20, 0x21, 0x22, 0x23, 0x37, 0x5F, 0xBB},   # X = P2
    1: {0x0B, 0x1E, 0x1F, 0x5F},                                                      # Y = P3
    2: {0x5F},                                                                        # Z = P4
}


def succ(codes, entry, pc, marks):
    n = len(codes)
    op = codes[pc]
    size = SIZES.get(op) or 1

    def s16(lo):
        return sext16(codes[lo] | (codes[lo + 1] << 8)) if lo + 1 < n else 0
    if op == 0xFF:
        return []
    if op == 0x02:
        return [pc + s16(pc + 1)]
    if op in (0x03, 0x04, 0x74):
        return [pc + size, pc + s16(pc + 1)]
    if op in (0x30, 0x31):
        return [pc + size, pc + s16(pc + 3)]
    if op == 0x49:
        return [entry]
    if op in (0x4A, 0x4B):
        return [pc + size, entry]
    if op in (0x57, 0x58):
        return [pc + s16(pc + 1 + 2 * d) for d in range(4)]
    if op == 0x78:
        return [pc + s16(pc + 1)]
    if op == 0x79:
        return [pc + size, pc + s16(pc + 1)]
    if op in (0x7B, 0x7C):
        return [pc + size, pc + s16(pc + 3)]
    if op == 0x7D:
        return sorted(marks)
    if op in (0x7E, 0x7F, 0x80, 0x81):
        return [pc + size] + sorted(marks)
    if size == 0:
        return []
    return [pc + size]


def program_marks(codes, entry):
    marks = set()
    while True:
        seen = set()
        work = [entry]
        found = set(marks)
        while work:
            pc = work.pop()
            if pc < 0 or pc >= len(codes) or pc in seen:
                continue
            seen.add(pc)
            if codes[pc] in (0x78, 0x79):
                found.add(pc + 3)
            if codes[pc] in (0x7B, 0x7C):
                found.add(pc + 5)
            work.extend(succ(codes, entry, pc, marks))
        if found == marks:
            return marks
        marks = found


def flow(codes, entry):
    marks = program_marks(codes, entry)
    res = {}
    for axis in range(3):
        at = {entry: {'I'}}
        work = [entry]
        while work:
            pc = work.pop()
            if pc < 0 or pc >= len(codes):
                continue
            op = codes[pc]
            out = set()
            for st in at[pc]:
                if op == 0x6A:
                    out.add('S')
                elif op in WR[axis]:
                    out.add('C')
                else:
                    out.add(st)
            for nx in succ(codes, entry, pc, marks):
                if nx < 0 or nx >= len(codes):
                    continue
                old = at.get(nx, set())
                if not out <= old:
                    at[nx] = old | out
                    work.append(nx)
        for pc, st in at.items():
            if codes[pc] == 0x6B + axis:
                res[pc] = frozenset(st)
    return res, marks


if __name__ == '__main__':
    sites = json.load(open(sys.argv[1]))['sites']
    progs = sorted(set((m, s, i) for m, s, i, pc, op in sites))
    # restrict to the 129 sites of the annex list (exclude 442 C[1], C[2])
    import csv
    rows = list(csv.reader(open('D:/development/repo/alundra-casaengine-project-converter/docs/plan-e19-sm-annexe/skipped-sites.tsv'), delimiter='\t'))
    lst = set((int(r[0]), r[2], int(r[3]), int(r[4])) for r in rows[1:] if r[5] in ('6A', '6B', '6C', '6D'))
    progs = sorted(set((m, s, i) for (m, s, i, pc) in lst))
    cnt = collections.Counter()
    reads = []
    for (m, s, i) in progs:
        d = load_codes(m)
        codes = d['Codes']
        entry = d['EventCodes%sTable' % s][i]
        r, marks = flow(codes, entry)
        for pc, st in sorted(r.items()):
            if (m, s, i, pc) in lst:
                reads.append((m, s, i, pc, codes[pc], ''.join(sorted(st))))
                cnt[(('X', 'Y', 'Z')[codes[pc] - 0x6B], ''.join(sorted(st)))] += 1
    print('programs', len(progs), 'reads', len(reads))
    print(dict(cnt))
    init = [(m, s, i, pc) for (m, s, i, pc, op, st) in reads if 'I' in st]
    print('reads with INIT:', init)
    # reads reached in lst but not by the flow (unreachable?) :
    rpcs = set((m, s, i, pc) for (m, s, i, pc, op, st) in reads)
    print('list reads not found by the flow:', sorted(x for x in lst if x not in rpcs and None))
    # list reads (6B..6D) in the annex list
    lr = [x for x in lst]
    nreads = 0
    for (m, s, i, pc) in lst:
        d = load_codes(m)
        if d['Codes'][pc] in (0x6B, 0x6C, 0x6D):
            nreads += 1
    print('list reads (6B/6C/6D):', nreads)
    # reader followed by 03/04 ?
    fol = collections.Counter()
    for (m, s, i, pc, op, st) in reads:
        d = load_codes(m)
        fol[d['Codes'][pc + 3]] += 1
    print('byte after each read:', {hex(k): v for k, v in fol.items()})
    # calls between: count 0x78 etc on paths? count programs containing calls
    calls = collections.Counter()
    for (m, s, i) in progs:
        d = load_codes(m)
        seen, ex = walk(d['Codes'], d['EventCodes%sTable' % s][i])
        for pc, op in seen.items():
            if op in (0x78, 0x79, 0x7B, 0x7C):
                calls[(m, s, i, pc, op)] += 1
    print('call sites in sm2 programs:', sorted(calls))
