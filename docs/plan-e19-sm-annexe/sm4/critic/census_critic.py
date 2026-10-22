"""Critic's own census: a from-scratch Python port of the C# walker AlundraStoryChainOpcodeAudit.WalkMap/WalkProgram/ReadRoots
(read at HEAD), run on the 483 maps of the export. Writes the sites of the 8 opcodes of sm4 and the 29 skipped opcodes.
Usage: python -B census_critic.py OUT_TSV"""
import json, os, re, sys

REPO = 'D:/development/repo/alundra-casaengine-project-converter'
PROJ = REPO + '/alundra-project'
SLOTS = 'ABCDEF'
src = open(REPO + '/Alundra/Events/EventOpcodeSizeTable.cs', encoding='utf-8').read()
SIZES = {}
NAMES = {}
for m in re.finditer(r'\{\s*0x([0-9A-Fa-f]{2}),\s*new\((\d+),\s*"([^"]*)"\)\s*\}', src):
    SIZES[int(m.group(1), 16)] = int(m.group(2))
    NAMES[int(m.group(1), 16)] = m.group(3)

SKIPPED = [0x15, 0x3C, 0x3D, 0x56, 0x5F, 0x60, 0x61, 0x66, 0x6A, 0x6B, 0x6C, 0x6D, 0x72, 0x7A, 0x88, 0x96, 0x97, 0x9E, 0xA9, 0xAA, 0xB2, 0xB3,
           0xB4, 0xB5, 0xB6, 0xB7, 0xB9, 0xBB, 0xC2]


def s16(lo, hi):
    v = (lo | (hi << 8)) & 0xFFFF
    return v - 0x10000 if v & 0x8000 else v


def walk_program(codes, entry, extra):
    n = len(codes)
    seen = {}
    marks = set()
    return_users = set()
    work = [entry]

    def B(i):
        return codes[i] if 0 <= i < n else 0

    while True:
        while work:
            pc = work.pop()
            while 0 <= pc < n and pc not in seen:
                op = codes[pc]
                seen[pc] = op
                if op not in SIZES or SIZES[op] == 0 or op == 0xFF:
                    break
                size = SIZES[op]
                jumped = False
                stop = False
                if op == 0x02:
                    pc += s16(B(pc + 1), B(pc + 2)); jumped = True
                elif op in (0x03, 0x04):
                    work.append(pc + s16(B(pc + 1), B(pc + 2)))
                elif op in (0x30, 0x31):
                    work.append(pc + s16(B(pc + 3), B(pc + 4)))
                elif op == 0x74:
                    work.append(pc + s16(B(pc + 1), B(pc + 2)))
                elif op in (0x57, 0x58):
                    for d in range(4):
                        work.append(pc + s16(B(pc + 1 + 2 * d), B(pc + 2 + 2 * d)))
                    stop = True
                elif op == 0x78:
                    marks.add(pc + 3)
                    pc += s16(B(pc + 1), B(pc + 2)); jumped = True
                elif op == 0x79:
                    marks.add(pc + 3)
                    work.append(pc + s16(B(pc + 1), B(pc + 2)))
                elif op in (0x7B, 0x7C):
                    marks.add(pc + 5)
                    work.append(pc + s16(B(pc + 3), B(pc + 4)))
                elif op == 0x7D:
                    return_users.add(pc); stop = True
                elif op in (0x7E, 0x7F, 0x80, 0x81):
                    return_users.add(pc)
                elif op == 0x49:
                    pc = entry; jumped = True
                elif op == 0x4B:
                    work.append(entry)
                elif op == 0x40:
                    if 2 <= B(pc + 1) <= 5:
                        extra.add((SLOTS[B(pc + 1)], B(pc + 2) & 0x7F))
                if stop:
                    break
                if not jumped:
                    pc += size
        if return_users:
            fresh = [m for m in marks if m not in seen]
            if fresh:
                work.extend(fresh)
                continue
        break
    return seen


def load_map(mid, idx):
    rel = idx[str(mid)].replace(chr(92), '/')
    base = os.path.splitext(os.path.basename(rel))[0]
    folder = os.path.dirname(rel)
    ev = json.load(open(PROJ + '/' + folder + '/events/' + base + '.events.json', encoding='utf-8'))
    tmdir = PROJ + '/' + folder + '/tilemap'
    tms = [f for f in os.listdir(tmdir) if f.endswith('.tileMap')]
    assert len(tms) == 1
    tm = json.load(open(tmdir + '/' + tms[0], encoding='utf-8'))
    return base, ev, tm


def tables(ev):
    return [ev['EventCodes%sTable' % s] for s in SLOTS]


def add_root(roots, tabs, slot, value):
    raw = int(value) if isinstance(value, str) else int(value)
    index = raw & 0x7F
    t = tabs[SLOTS.index(slot)]
    if index != 0 and index < len(t) and t[index] != 0:
        roots.append((slot, index))


def roots_of(ev, tm):
    tabs = tables(ev)
    roots = []
    for layer in tm['object_layers']:
        if layer['name'] == 'MapEvents':
            for o in layer['objects']:
                add_root(roots, tabs, 'B', o['custom_properties']['EventCodesBIndex'])
    for layer in tm['object_layers']:
        if layer['name'] != 'Entities':
            continue
        for o in layer['objects']:
            p = o['custom_properties']
            add_root(roots, tabs, 'A', p['EventCodesA_LoadIndex'])
            add_root(roots, tabs, 'C', p['EventCodesC_TickIndex'])
            add_root(roots, tabs, 'D', p['EventCodesD_TouchIndex'])
            add_root(roots, tabs, 'E', p['EventCodesE_DeactivateIndex'])
            add_root(roots, tabs, 'F', p['EventCodesF_InteractIndex'])
    return roots


def walk_map(ev, tm):
    tabs = tables(ev)
    codes = ev['Codes']
    reached = []
    done = set()
    todo = list(roots_of(ev, tm))
    qi = 0
    while qi < len(todo):
        slot, index = todo[qi]; qi += 1
        if (slot, index) in done:
            continue
        done.add((slot, index))
        extra = set()
        seen = walk_program(codes, tabs[SLOTS.index(slot)][index], extra)
        for pc in sorted(seen):
            reached.append((slot, index, pc, seen[pc]))
        for (os_, oi) in sorted(extra):
            t = tabs[SLOTS.index(os_)]
            if (os_, oi) not in done and oi < len(t) and t[oi] != 0:
                todo.append((os_, oi))
    return reached


def main(out):
    idx = json.load(open(PROJ + '/Maps/world-index.json', encoding='utf-8'))
    rows = []
    total_instr = 0
    opcodes_seen = set()
    for mid in sorted(int(k) for k in idx):
        base, ev, tm = load_map(mid, idx)
        reached = walk_map(ev, tm)
        total_instr += len(reached)
        for slot, index, pc, op in reached:
            opcodes_seen.add(op)
            if op in SKIPPED:
                rows.append((mid, slot, index, pc, op))
    with open(out, 'w') as f:
        f.write('map\tslot\tprogram\tpc\topcode\n')
        for r in rows:
            f.write('%d\t%s\t%d\t%d\t0x%02X\n' % r)
    print('maps', len(idx), 'instructions(with duplicates across programs)', total_instr, 'distinct opcodes', len(opcodes_seen))
    print('skipped sites', len(rows), 'maps with skipped', len({r[0] for r in rows}), 'distinct (map,slot,program?)/(map,pc):',
          len({(r[0], r[3]) for r in rows}), 'opcodes', len({r[4] for r in rows}))


if __name__ == '__main__':
    main(sys.argv[1])
