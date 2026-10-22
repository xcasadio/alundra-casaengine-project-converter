"""Critic-own: path-sensitive (CFG) analysis of the Result value that reaches each sm3 site TODAY (the 8 sm3 opcodes skipped, so they do not write Result).
Abstract state = (pc, result, mark34). Predicates / Result writers of the DLL are non-deterministic (0 or 1). Flag branches, directional jumps and the 0x74 counter
are non-deterministic (both edges). Restart opcodes (0x49/0x4A/0x4B) go back to the program entry.
Mode A: first run only (initial Result 0, FF ends).  Mode B: the program may be re-entered after FF / after a restart with the Result it left (over-approximation)."""
import csv
import sys
import mycorpus as c

WRITERS = {0x07, 0x2C, 0x2E, 0x2F, 0x33, 0x34, 0x3B, 0x3E, 0x3F, 0x43, 0x44, 0x52, 0x6E, 0x6F, 0x70, 0x71, 0x82, 0x83, 0x84, 0x87, 0x8C, 0x8D,
           0x95, 0x99, 0x9A, 0x9F, 0xA8, 0xAD, 0xB1, 0xB8, 0xBA}


def rel16(codes, pc, o):
    return ((codes[pc + o + 1] << 8) | codes[pc + o]) if True else 0


def s16(x):
    x &= 0xFFFF
    return x - 0x10000 if x & 0x8000 else x


def successors(codes, pc, r, mark, entry, today_sm3=True):
    """yield (next_pc, new_r, new_mark) ; None pc = end of path"""
    op = codes[pc]
    sz = c.SIZES.get(op, 0)
    if op == 0xFF:
        yield (None, r, mark)
        return
    if sz <= 0:
        yield (None, r, mark)
        return
    nxt = pc + sz
    b = lambda k: codes[pc + k] if pc + k < len(codes) else 0
    j16 = lambda k: s16(b(k) | (b(k + 1) << 8))
    if op == 0x02:
        yield (pc + j16(1), r, mark)
    elif op == 0x03:
        yield (pc + j16(1), r, mark) if r != 0 else (nxt, r, mark)
    elif op == 0x04:
        yield (pc + j16(1), r, mark) if r == 0 else (nxt, r, mark)
    elif op in (0x30, 0x31):
        yield (pc + j16(3), r, mark)
        yield (nxt, r, mark)
    elif op == 0x49:
        yield (entry, r, mark)
    elif op == 0x4A:
        yield (entry, r, mark) if r != 0 else (nxt, r, mark)
    elif op == 0x4B:
        yield (entry, r, mark) if r == 0 else (nxt, r, mark)
    elif op in (0x57, 0x58):
        for d in range(4):
            yield (pc + s16(b(1 + 2 * d) | (b(2 + 2 * d) << 8)), r, mark)
    elif op == 0x74:
        yield (pc + j16(1), r, mark)
        yield (nxt, r, mark)
    elif op == 0x78:
        yield (pc + j16(1), r, pc + 3)
    elif op == 0x79:
        if r != 0:
            yield (pc + j16(1), r, pc + 3)
        else:
            yield (nxt, r, mark)
    elif op in (0x7B, 0x7C):
        yield (pc + j16(3), r, pc + 5)
        yield (nxt, r, mark)
    elif op == 0x7D:
        yield ((mark if mark is not None else None), r, mark)
    elif op == 0x7E:
        yield ((mark if mark is not None else None), r, mark) if r != 0 else (nxt, r, mark)
    elif op == 0x7F:
        yield ((mark if mark is not None else None), r, mark) if r == 0 else (nxt, r, mark)
    elif op in (0x80, 0x81):
        yield ((mark if mark is not None else None), r, mark)
        yield (nxt, r, mark)
    elif op in WRITERS:
        yield (nxt, 0, mark)
        yield (nxt, 1, mark)
    else:
        yield (nxt, r, mark)


def analyse(codes, entry, sites, reenter):
    seen = set()
    stack = [(entry, 0, None)]
    arrivals = {s: set() for s in sites}
    parent = {}
    while stack:
        st = stack.pop()
        if st in seen:
            continue
        seen.add(st)
        pc, r, mark = st
        if pc is None or pc < 0 or pc >= len(codes):
            continue
        if pc in arrivals:
            arrivals[pc].add(r)
        for (npc, nr, nm) in successors(codes, pc, r, mark, entry):
            if npc is None:
                if reenter and codes[pc] == 0xFF:
                    stack.append((entry, nr, None))
                continue
            ns = (npc, nr, nm)
            if ns not in seen:
                parent.setdefault(ns, st)
                stack.append(ns)
    return arrivals, parent, seen


def witness(parent, seen, codes, site, want):
    for st in seen:
        if st[0] == site and st[1] == want:
            path = []
            cur = st
            n = 0
            while cur is not None and n < 400:
                path.append(cur)
                cur = parent.get(cur)
                n += 1
            return list(reversed(path))
    return None


if __name__ == '__main__':
    rows = list(csv.DictReader(open('../../sm3/corpus-lines-sm3.tsv', encoding='utf-8'), delimiter='\t'))
    byprog = {}
    for r in rows:
        byprog.setdefault((int(r['map']), r['slot'], int(r['program'])), []).append((int(r['pc']), r['opcode'], r['operands']))
    for (mid, slot, prog), sites in sorted(byprog.items()):
        b, d = c.load(mid)
        codes = d['Codes']
        entry = d['EventCodes%sTable' % slot][prog]
        pcs = [p for p, _, _ in sites]
        A, parA, seenA = analyse(codes, entry, pcs, False)
        B, parB, seenB = analyse(codes, entry, pcs, True)
        for p, op, ops in sites:
            print('%3d %s[%2d] @%-5d %s %-6s  first-run Result at arrival: %-8s  with re-entry: %-8s' % (mid, slot, prog, p, op, ops, sorted(A[p]) if A[p] else 'UNREACHED', sorted(B[p]) if B[p] else 'UNREACHED'))
