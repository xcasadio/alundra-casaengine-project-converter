"""Critic: decode a stretch of a map's Codes with the DLL size table.  usage: decode.py <mapIndex> <from> <to>"""
import json, glob, re, sys

root = "D:/development/repo/alundra-casaengine-project-converter"
s = open(root + '/Alundra/Events/EventOpcodeSizeTable.cs', encoding='utf-8-sig').read()
tab = {int(a, 16): (int(b), c) for a, b, c in re.findall(r'\{\s*0x([0-9A-Fa-f]{2}),\s*new\((-?\d+),\s*"([^"]*)"', s)}


def load(mi):
    for p in glob.glob(root + '/alundra-project/Maps/**/*.events.json', recursive=True):
        d = json.load(open(p, encoding='utf-8-sig'))
        if d['MapIndex'] == mi:
            return d
    raise SystemExit('no map')


def s16(c, i):
    v = c[i] | (c[i + 1] << 8)
    return v - 65536 if v >= 32768 else v


def main():
    mi, lo, hi = int(sys.argv[1]), int(sys.argv[2]), int(sys.argv[3])
    d = load(mi)
    c = d['Codes']
    for slot in 'ABCDEF':
        t = d['EventCodes%sTable' % slot]
        print(slot, t)
    pc = lo
    while pc < hi:
        op = c[pc]
        z, name = tab.get(op, (1, '?'))
        z = max(z, 1)
        ops = ' '.join('%02X' % b for b in c[pc + 1:pc + z])
        extra = ''
        if op in (2, 3, 4, 0x74):
            extra = ' -> %d' % (pc + s16(c, pc + 1))
        if op in (0x30, 0x31):
            extra = ' -> %d' % (pc + s16(c, pc + 3))
        print('%5d  %02X %-16s %s%s' % (pc, op, ops, name, extra))
        pc += z


main()
