"""Who sets (0x05) a flag across the corpus: linear decode of every program of every map (reachable or not: the size table walk from each entry).
Usage: python -B flag_setters.py FLAG [FLAG ...]  (flag as the 16-bit operand, e.g. 257 or 0x87D0)"""
import json, re, sys, os, glob
REPO = "D:/development/repo/alundra-casaengine-project-converter"


def sizes():
    s = {}
    for line in open(REPO + "/Alundra/Events/EventOpcodeSizeTable.cs", encoding="utf-8-sig"):
        m = re.search(r"\{\s*0x([0-9A-Fa-f]{2}),\s*new\((\d+),\s*\"([^\"]*)\"", line)
        if m:
            s[int(m.group(1), 16)] = int(m.group(2))
    return s


SIZES = sizes()
wanted = [int(x, 0) for x in sys.argv[1:]]
files = sorted(glob.glob(REPO + '/alundra-project/Maps/*/*/events/*.events.json'))
for f in files:
    d = json.load(open(f, encoding='utf-8'))
    codes = d['Codes']
    seen = set()
    for slot in 'ABCDEF':
        for e, entry in enumerate(d['EventCodes%sTable' % slot]):
            pc = entry
            if pc < 0:
                continue
            while pc < len(codes) and pc not in seen:
                seen.add(pc)
                op = codes[pc]
                sz = SIZES.get(op, 1) or 1
                if op in (0x05, 0x06, 0x36, 0x35, 0x30, 0x31) and pc + 2 < len(codes):
                    flag = codes[pc + 1] | (codes[pc + 2] << 8)
                    if flag in wanted:
                        print(os.path.basename(f), '%s[%d]' % (slot, e), '@%d' % pc, '%02X' % op, '%04X' % flag)
                if op == 0xFF:
                    break
                pc += sz
