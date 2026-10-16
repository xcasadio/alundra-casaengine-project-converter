#!/usr/bin/env python
"""Static search for suspend-free cycles in the event programs (label script2). Read-only.

A cycle whose instructions never suspend would spin until the production loop guard (1024 opcodes, AlundraEventProgramRunner.cs:137)
cuts the call. Graph = reachable instructions of each root program (same roots and size table as census.py), edges =
fall-through plus every jump target (both branches of conditionals); the return family 0x7D/0x7E/0x7F/0x80/0x81 gets NO
return edge (under-approximation: a cycle through a stored-return is not found). Suspending opcodes (a handler that can return 0 in the
DLL) are removed. INFERENCE only: it does not run the DLL.
Output: cycles.tsv (map, slot, program, entry, cycle_pc_sample, ops) and a summary on stdout.
"""
import json, os, re, sys, collections
sys.setrecursionlimit(100000)
exec(open(os.path.join(os.path.dirname(os.path.abspath(__file__)), 'census.py')).read().split('def main():')[0])

# Opcodes whose DLL handler can return 0 (suspend or end the call), from AlundraEventProgramRunner.Dispatch:
SUSPEND = {0x00, 0x0B, 0x0D, 0x1C, 0x1D, 0x1E, 0x1F, 0x20, 0x21, 0x22, 0x23, 0x24, 0x25, 0x26, 0x35, 0x36, 0x37, 0x39, 0x44,
           0x47, 0x48, 0x5C, 0xC4, 0xFF}
RETURN_FAMILY = {0x7D, 0x7E, 0x7F, 0x80, 0x81}


def edges_for(codes, pc, op, entry):
    size = SIZES[op]
    n = len(codes)
    out = []
    fall = pc + size
    if op == 0x02:
        out.append(pc + s16(codes, pc + 1, pc + 2))
    elif op in (0x03, 0x04):
        out += [fall, pc + s16(codes, pc + 1, pc + 2)]
    elif op in (0x30, 0x31):
        out += [fall, pc + s16(codes, pc + 3, pc + 4)]
    elif op == 0x74:
        out += [fall, pc + s16(codes, pc + 1, pc + 2)]
    elif op in (0x57, 0x58):
        out += [pc + s16(codes, pc + 1 + 2 * d, pc + 2 + 2 * d) for d in range(4)]
    elif op == 0x78:
        out.append(pc + s16(codes, pc + 1, pc + 2))
    elif op == 0x79:
        out += [fall, pc + s16(codes, pc + 1, pc + 2)]
    elif op in (0x7B, 0x7C):
        out += [fall, pc + s16(codes, pc + 3, pc + 4)]
    elif op == 0x7D:
        pass
    elif op in (0x7E, 0x7F, 0x80, 0x81):
        out.append(fall)
    elif op == 0x49:
        out.append(entry)
    elif op in (0x4A, 0x4B):
        out += [fall, entry]
    else:
        out.append(fall)
    return [t for t in out if 0 <= t < n]


def find_cycle(codes, entry):
    """Return a list of pcs forming a suspend-free cycle reachable from entry, or None."""
    n = len(codes)
    color = {}
    stack = []
    # iterative DFS over non-suspending nodes
    def succs(pc):
        op = codes[pc]
        if op not in SIZES or SIZES[op] == 0 or op in SUSPEND:
            return []
        return edges_for(codes, pc, op, entry)

    it = {}
    work = [entry]
    path = []
    while work:
        pc = work[-1]
        if pc not in color:
            color[pc] = 1
            path.append(pc)
            it[pc] = iter(succs(pc))
        advanced = False
        for t in it[pc]:
            c = color.get(t)
            if c == 1:
                i = path.index(t)
                return path[i:]
            if c is None:
                work.append(t)
                advanced = True
                break
        if not advanced:
            color[pc] = 2
            path.pop()
            work.pop()
    return None


def main2():
    index = json.load(open(PROJECT + '/Maps/world-index.json', encoding='utf-8'))
    stats = {k: collections.Counter() for k in ('raw_nonzero', 'bit80_set', 'bit80_clear_nonzero', 'native_only_raw_nonzero', 'root_not_in_table')}
    rows = []
    programs = 0
    for mid_s, rel in sorted(index.items(), key=lambda kv: int(kv[0])):
        mid = int(mid_s)
        rel = rel.replace('\\', '/')
        folder = os.path.dirname(PROJECT + '/' + rel)
        base = os.path.splitext(os.path.basename(rel))[0]
        doc = json.load(open(folder + '/events/' + base + '.events.json', encoding='utf-8'))
        tm_dir = folder + '/tilemap'
        tms = [f for f in os.listdir(tm_dir) if f.endswith('.tileMap')]
        tilemap = json.load(open(tm_dir + '/' + tms[0], encoding='utf-8'))
        roots = read_roots(tilemap, doc, stats)
        # roots incl. 0x40 targets: reuse walk_map to expand them
        reached = walk_map(doc, roots)
        all_roots = sorted({(s, i) for s, i, pc, op in reached})
        codes = doc['Codes']
        for slot, idx in all_roots:
            programs += 1
            entry = table_for(doc, slot)[idx]
            cyc = find_cycle(codes, entry)
            if cyc:
                rows.append((mid, base, slot, idx, entry, cyc[0], len(cyc), ' '.join('%02X' % codes[p] for p in cyc[:14])))
    with open(OUT + '/cycles.tsv', 'w', encoding='utf-8', newline='\n') as f:
        f.write('map\tmap_name\tslot\tprogram\tentry\tcycle_start_pc\tcycle_len\tcycle_opcodes\n')
        for r in rows:
            f.write('%d\t%s\t%s\t%d\t%d\t%d\t%d\t%s\n' % r)
    print('programs analysed', programs, 'with a suspend-free cycle', len(rows), 'in', len({r[0] for r in rows}), 'maps')
    print('chain30 maps with cycles:', sorted({r[0] for r in rows if r[0] in {389,390,476,478,392,391,416,163,162,165,164,172,169,170,171,173,174,175,10,179,176,177,178,180,181,182,135,183,184,185}}))
    c = collections.Counter(r[7] for r in rows)
    for k, v in c.most_common(8):
        print(v, k)


main2()
