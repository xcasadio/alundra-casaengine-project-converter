"""Event-program decoding helpers over the exported maps (read-only). Opcode sizes and names come from the DLL table
(Alundra/Events/EventOpcodeSizeTable.cs), map data from data-extracted/data/map_N.json."""
import json, re, sys, io
P = 'D:/development/repo/alundra-casaengine-project-converter/'
SIZES = {}
for m in re.finditer(r'\{\s*0x([0-9A-Fa-f]{2}),\s*new\((\d+),\s*"([^"]*)"\)', open(P + 'Alundra/Events/EventOpcodeSizeTable.cs', encoding='utf-8').read()):
    SIZES[int(m.group(1), 16)] = (int(m.group(2)), m.group(3))
_cache = {}
SLOTS = 'ABCDEF'

def load(mid):
    if mid not in _cache:
        d = json.load(open(P + 'data-extracted/data/map_%d.json' % mid, encoding='utf-8'))
        _cache[mid] = d
    return _cache[mid]

def program_start(mid, slot, index):
    ec = load(mid)['SpriteInfo']['EventCodes']
    tab = ec['EventCodes%sTable' % slot]
    i = index & 0x7F
    return tab[i] if i < len(tab) else None

def all_starts(mid):
    ec = load(mid)['SpriteInfo']['EventCodes']
    starts = set()
    for s in SLOTS:
        for o in ec['EventCodes%sTable' % s]:
            if 0 < o < len(ec['Codes']):
                starts.add(o)
    return sorted(starts)

def decode(mid, start, limit=400):
    """returns list of (pc, opcode, name, operands) from start until Break (0) / End or next program start"""
    ec = load(mid)['SpriteInfo']['EventCodes']
    codes = ec['Codes']
    starts = all_starts(mid)
    out = []
    pc = start
    n = 0
    while pc < len(codes) and n < limit:
        op = codes[pc]
        sz, name = SIZES.get(op, (0, '?'))
        if sz <= 0:
            out.append((pc, op, 'UNDECODABLE', []))
            break
        out.append((pc, op, name, list(codes[pc + 1: pc + sz])))
        pc += sz
        n += 1
        if pc in starts and pc != start:
            break
    return out

def show(mid, slot, index, limit=400):
    st = program_start(mid, slot, index)
    print('--- map %d slot %s index %d (raw %d) start %s' % (mid, slot, index & 0x7F, index, st))
    if st is None:
        return
    for pc, op, name, ops in decode(mid, st, limit):
        print('  %5d  %02X %-48s %s' % (pc, op, name, ops))

if __name__ == '__main__':
    sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')
    mid = int(sys.argv[1]); slot = sys.argv[2]; idx = int(sys.argv[3])
    show(mid, slot, idx)
