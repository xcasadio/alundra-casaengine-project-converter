"""Read-only access to the exported event-program corpus, at the HEAD names (Alundra/Events/EventOpcodeSizeTable.cs).
Own copy for the E19.sm2 preparation. Never writes in the repository. Python -B.

Usage: python -B corpus2.py MAP SLOT PROGRAM  -> prints the linear decode of that program from its entry up to the first 0xFF
"""
import json, os, re, sys

REPO = 'D:/development/repo/alundra-casaengine-project-converter'
PROJECT = REPO + '/alundra-project'
SLOTS = 'ABCDEF'
TABLE_KEYS = {s: 'EventCodes%sTable' % s for s in SLOTS}

_src = open(REPO + '/Alundra/Events/EventOpcodeSizeTable.cs', encoding='utf-8').read()
SIZES = {}
NAMES = {}
for m in re.finditer(r'\{\s*0x([0-9A-Fa-f]{2}),\s*new\((\d+),\s*"([^"]*)"\)\s*\}', _src):
    SIZES[int(m.group(1), 16)] = int(m.group(2))
    NAMES[int(m.group(1), 16)] = m.group(3)

_index = json.load(open(PROJECT + '/Maps/world-index.json', encoding='utf-8'))


def map_ids():
    return sorted(int(k) for k in _index)


def paths(mid):
    rel = _index[str(mid)].replace(chr(92), '/')
    base = os.path.splitext(os.path.basename(rel))[0]
    folder = PROJECT + '/' + os.path.dirname(rel)
    return base, folder


def load(mid):
    base, folder = paths(mid)
    p = folder + '/events/' + base + '.events.json'
    return base, json.load(open(p, encoding='utf-8'))


def s16(codes, lo):
    v = codes[lo] | (codes[lo + 1] << 8) if lo + 1 < len(codes) else 0
    return v - 0x10000 if v >= 0x8000 else v


def decode_one(codes, pc):
    op = codes[pc]
    size = SIZES.get(op, 1) or 1
    return op, size, list(codes[pc + 1:pc + size])


def decode(codes, start, count=40, stop_at_end=True):
    out = []
    pc = start
    while len(out) < count and pc < len(codes):
        op, size, ops = decode_one(codes, pc)
        out.append((pc, op, ops))
        pc += size
        if stop_at_end and op == 0xFF:
            break
    return out


def fmt(pc, op, ops, mark=''):
    return '%s%5d  %02X %-34s %s' % (mark, pc, op, NAMES.get(op, '?'), ' '.join('%02X' % b for b in ops))


if __name__ == '__main__':
    mid, slot, prog = int(sys.argv[1]), sys.argv[2], int(sys.argv[3])
    base, d = load(mid)
    entry = d[TABLE_KEYS[slot]][prog]
    print(base, slot, prog, 'entry', entry)
    for pc, op, ops in decode(d['Codes'], entry, count=100000):
        print(fmt(pc, op, ops))
