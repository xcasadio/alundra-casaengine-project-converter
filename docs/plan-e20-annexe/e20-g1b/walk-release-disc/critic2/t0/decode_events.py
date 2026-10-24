#!/usr/bin/env python3
"""Read-only linear decoder of the exported event programs (alundra-project/Maps/*/*/events/*.events.json).

Instruction sizes come from Alundra/Events/EventOpcodeSizeTable.cs (parsed with a regex, so the table is the DLL's).
Slots: A=load, B=map, C=tick, D=touch, E=deactivate, F=interact (EventCodes<X>Table of the JSON; entry = index into Codes).
A program is swept linearly from its start to the next program start of any table (both branches of every conditional are therefore
seen; a 0xFF inside is only a branch exit). Walk radii: 0x0B = v2 | v3 << 8 (px, WalkUntilBeyondRadius, EventProgramRunner.cs:2901-2925),
0x1E/0x1F = v1 | v2 << 8 (Walk, EventProgramRunner.cs:2708-2740).

Usage: decode_events.py MAP [MAP ...]  -> prints the interesting instructions (walks, placements, spawns, direction, logic entity, flags 0x62/0x63).
"""
import glob, json, os, re, sys

ROOT = 'D:/development/repo/alundra-casaengine-project-converter'
SIZE_FILE = ROOT + '/Alundra/Events/EventOpcodeSizeTable.cs'


def load_sizes():
    sizes = {}
    for m in re.finditer(r'\{\s*0x([0-9A-Fa-f]{2}),\s*new\((\d+),\s*"([^"]*)"\)\s*\}', open(SIZE_FILE, encoding='utf-8-sig').read()):
        sizes[int(m.group(1), 16)] = (int(m.group(2)), m.group(3))
    return sizes


SIZES = load_sizes()


def events_path(mapidx):
    for f in glob.glob(ROOT + '/alundra-project/Maps/*/*/events/*.events.json'):
        f = f.replace(chr(92), '/')
        if f.endswith('-%d.events.json' % mapidx):
            return f
    return None


def load_map(mapidx):
    p = events_path(mapidx)
    return json.load(open(p, encoding='utf-8')) if p else None


def programs(doc):
    """[(slot_letter, index_in_table, start)] over the six tables."""
    out = []
    for slot in 'ABCDEF':
        tab = doc['EventCodes%sTable' % slot]
        for i, s in enumerate(tab):
            out.append((slot, i, s))
    return out


def sweep(doc, start, end):
    codes = doc['Codes']
    pc = start
    ins = []
    if start < 0 or start >= len(codes):
        return ins  # a table entry outside Codes (seen in the F table of map 177): nothing to sweep.
    while pc < end and pc < len(codes):
        op = codes[pc]
        size = SIZES.get(op, (0, '?'))[0]
        if size < 1 or pc + size > len(codes):
            ins.append((pc, op, None, True))
            break
        ins.append((pc, op, codes[pc:pc + size], False))
        pc += size
    return ins


def all_programs(doc):
    progs = programs(doc)
    starts = sorted({s for _, _, s in progs} | {len(doc['Codes'])})
    nxt = {s: starts[i + 1] for i, s in enumerate(starts[:-1])}
    res = []
    seen = set()
    for slot, i, s in progs:
        if (slot, s) in seen:
            continue
        seen.add((slot, s))
        res.append((slot, i, s, nxt.get(s, len(doc['Codes']))))
    return res


INTEREST = {0x0B: 'walk0B', 0x1E: 'walk1E', 0x1F: 'walk1F', 0x64: 'setpos', 0x65: 'addpos', 0x8A: 'spawnXY', 0x8B: 'spawnRel', 0x2D: 'spawnRec',
            0x43: 'logic=', 0x42: 'logic=hero', 0x63: 'clearflags', 0x62: 'setflags', 0x53: 'warp', 0x5A: 'walkanim5A', 0x5B: 'walkanim5B',
            0x08: 'dir+=', 0x09: 'dir=', 0x0A: 'dirrev', 0x2E: 'destroy', 0x24: 'waitAdj', 0x27: 'face', 0x1D: 'walkToward?'}


def describe(op, v):
    if op == 0x0B:
        return f'walk0B anim={v[1]} r={v[2] | v[3] << 8}px'
    if op in (0x1E, 0x1F):
        return f'walk{op:02X} d={v[1] | v[2] << 8}px'
    if op == 0x64:
        return f'setpos sel={v[1]} x={v[2] | v[3] << 8} y={v[4] | v[5] << 8} z={v[6] | v[7] << 8}'
    if op == 0x65:
        return f'addpos sel={v[1]} dx={v[2] | v[3] << 8} dy={v[4] | v[5] << 8} dz={v[6] | v[7] << 8}'
    if op == 0x8A:
        return f'spawnXY rec={v[1]} x={v[2] | v[3] << 8} y={v[4] | v[5] << 8} {list(v)}'
    if op == 0x8B:
        return f'spawnRel sel={v[1]} rec={v[2]} {list(v)}'
    if op == 0x2D:
        return f'spawnRec rec={v[1]}'
    return INTEREST[op] + ' ' + str(list(v[1:]))


def main():
    for m in [int(a) for a in sys.argv[1:]]:
        doc = load_map(m)
        if doc is None:
            print(f'map {m}: no events file')
            continue
        print(f'== map {m}: Codes={len(doc["Codes"])}')
        for slot, i, s, e in all_programs(doc):
            ins = [x for x in sweep(doc, s, e) if x[1] in INTEREST]
            if not ins:
                continue
            print(f' -- slot {slot}[{i}] @{s}..{e}')
            for pc, op, v, bad in ins:
                print(f'    @{pc}: 0x{op:02X} ' + describe(op, v))


if __name__ == '__main__':
    main()
