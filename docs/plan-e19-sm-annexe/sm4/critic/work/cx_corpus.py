"""Critic's own corpus reader (written from scratch). Read-only on alundra-project/."""
import json, os, re
REPO = 'D:/development/repo/alundra-casaengine-project-converter'
PROJECT = REPO + '/alundra-project'
IDX = json.load(open(PROJECT + '/Maps/world-index.json', encoding='utf-8'))
src = open(REPO + '/Alundra/Events/EventOpcodeSizeTable.cs', encoding='utf-8').read()
SZ = {}
NM = {}
for m in re.finditer(r'\{\s*0x([0-9A-Fa-f]{2}),\s*new\((\d+),\s*"([^"]*)"\)\s*\}', src):
    SZ[int(m.group(1), 16)] = int(m.group(2))
    NM[int(m.group(1), 16)] = m.group(3)
SL = 'ABCDEF'
BS = chr(92)


def load(mid):
    rel = IDX[str(mid)].replace(BS, '/')
    base = os.path.splitext(os.path.basename(rel))[0]
    folder = PROJECT + '/' + os.path.dirname(rel)
    p = folder + '/events/' + base + '.events.json'
    return base, json.load(open(p, encoding='utf-8')), folder


def programs(d):
    out = []
    for s in SL:
        t = d.get('EventCodes%sTable' % s) or []
        n = d['Header']['EventCodes%sSize' % s] // 2
        for i, e in enumerate(t[:n]):
            if 0 <= e < len(d['Codes']):
                out.append((s, i, e))
    return out


def sweep(codes, start, end):
    pc = start
    seq = []
    end = min(end, len(codes))
    while pc < end:
        op = codes[pc]
        sz = SZ.get(op, 1) or 1
        seq.append((pc, op, codes[pc + 1:pc + sz]))
        pc += sz
    return seq
