"""Read-only access to the exported event-program corpus (alundra-project/Maps/**/events/*.events.json).
Usage: python -B corpus.py MAP [SLOT PROGRAM]    -> prints the structure / the decoded program."""
import json, os, re, sys

REPO = 'D:/development/repo/alundra-casaengine-project-converter'
PROJECT = REPO + '/alundra-project'
SLOTS = 'ABCDEF'

_src = open(REPO + '/Alundra/Scripts/EventOpcodeSizeTable.cs', encoding='utf-8').read()
SIZES = {}
NAMES = {}
for m in re.finditer(r'\{\s*0x([0-9A-Fa-f]{2}),\s*new\((\d+),\s*"([^"]*)"\)\s*\}', _src):
    SIZES[int(m.group(1), 16)] = int(m.group(2))
    NAMES[int(m.group(1), 16)] = m.group(3)

_index = json.load(open(PROJECT + '/Maps/world-index.json', encoding='utf-8'))


def load(mid):
    rel = _index[str(mid)].replace(chr(92), '/')
    base = os.path.splitext(os.path.basename(rel))[0]
    p = PROJECT + '/' + os.path.dirname(rel) + '/events/' + base + '.events.json'
    return base, json.load(open(p, encoding='utf-8'))


def decode(codes, start, count=40, stop_at_end=True):
    out = []
    pc = start
    while len(out) < count and pc < len(codes):
        op = codes[pc]
        size = SIZES.get(op, 1) or 1
        ops = ' '.join('%02X' % b for b in codes[pc + 1:pc + size])
        out.append((pc, op, ops))
        pc += size
        if stop_at_end and op == 0xFF:
            break
    return out


if __name__ == '__main__':
    mid = int(sys.argv[1])
    base, d = load(mid)
    print(base, [k for k in d.keys()])
    for k, v in d.items():
        if k != 'Codes':
            print(k, str(v)[:600])
    print('codes', len(d['Codes']))


TABLE_KEYS = {s: 'EventCodes%sTable' % s for s in SLOTS}


def context(mid, slot, prog, pc, before=6, after=5):
    base, d = load(mid)
    codes = d['Codes']
    entry = d[TABLE_KEYS[slot]][prog]
    seq = decode(codes, entry, count=2000, stop_at_end=False)
    idx = [i for i, (p, o, ops) in enumerate(seq) if p == pc]
    if not idx:
        return None
    i = idx[0]
    lo = max(0, i - before)
    return [(p, o, ops, p == pc) for (p, o, ops) in seq[lo:i + after + 1]], entry


def show(mid, slot, prog, pc, before=6, after=5):
    r = context(mid, slot, prog, pc, before, after)
    if r is None:
        print('map %d %s[%d] @%d: pc not on the linear decode of the program' % (mid, slot, prog, pc))
        return
    rows, entry = r
    print('map %d %s[%d] entry=%d  site @%d' % (mid, slot, prog, entry, pc))
    for p, o, ops, hit in rows:
        print('   %s%5d  %02X %-34s %s' % ('>>' if hit else '  ', p, o, NAMES.get(o, '?'), ops))
