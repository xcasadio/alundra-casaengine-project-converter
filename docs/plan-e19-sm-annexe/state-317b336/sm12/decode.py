import json, re, sys, os
REPO = 'D:/development/repo/alundra-casaengine-project-converter'
# sizes from EventOpcodeSizeTable at HEAD
t = open(REPO + '/Alundra/Events/EventOpcodeSizeTable.cs', encoding='utf-8-sig').read()
SIZES = {int(m.group(1), 16): int(m.group(2)) for m in re.finditer(r'\{ 0x([0-9A-Fa-f]{2}), new\((\d+),', t)}
NAMES = {int(m.group(1), 16): m.group(2) for m in re.finditer(r'\{ 0x([0-9A-Fa-f]{2}), new\(\d+, "([^"]*)"', t)}


def load(mapid):
    idx = json.load(open(REPO + '/alundra-project/Maps/world-index.json', encoding='utf-8'))
    rel = idx[str(mapid)].replace('\\', '/')
    folder = os.path.dirname(REPO + '/alundra-project/' + rel)
    name = os.path.splitext(os.path.basename(rel))[0]
    return json.load(open(folder + '/events/' + name + '.events.json', encoding='utf-8'))


def dump(mapid, slot, prog, lo=None, hi=None, maxn=60):
    d = load(mapid)
    table = d['EventCodes%sTable' % slot]
    codes = d['Codes']
    pc = table[prog]
    out = []
    n = 0
    while pc < len(codes) and n < maxn:
        op = codes[pc]
        sz = SIZES.get(op, 1) or 1
        if lo is None or lo <= pc <= hi:
            out.append('@%d %02X %s | %s' % (pc, op, ' '.join('%02X' % b for b in codes[pc + 1:pc + sz]), NAMES.get(op, '?')))
        if op == 0xFF:
            break
        pc += sz
        n += 1
        if hi is not None and pc > hi:
            break
    return out, table[prog]


if __name__ == '__main__':
    mapid = int(sys.argv[1]); slot = sys.argv[2]; prog = int(sys.argv[3])
    lo = int(sys.argv[4]) if len(sys.argv) > 4 else None
    hi = int(sys.argv[5]) if len(sys.argv) > 5 else None
    lines, start = dump(mapid, slot, prog, lo, hi)
    print('map', mapid, slot, prog, 'entry', start)
    print('\n'.join(lines))
