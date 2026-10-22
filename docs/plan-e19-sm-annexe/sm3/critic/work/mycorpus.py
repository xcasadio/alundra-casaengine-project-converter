"""Critic-own corpus scan (from scratch). Reads alundra-project/Maps/**/events/*.events.json (read-only).
Sizes come from the BINARY handlers' returned v0 (not from the DLL table): for the 8 skipped-sm3 opcodes we only need alignment, so sizes are
read from the DLL size table text (needed for alignment of all other opcodes)."""
import json
import os
import re
import sys

REPO = 'D:/development/repo/alundra-casaengine-project-converter'
PROJECT = REPO + '/alundra-project'
SLOTS = 'ABCDEF'
src = open(REPO + '/Alundra/Events/EventOpcodeSizeTable.cs', encoding='utf-8').read()
SIZES = {}
for mt in re.finditer(r'\{\s*0x([0-9A-Fa-f]{2}),\s*new\((\d+),\s*"([^"]*)"\)\s*\}', src):
    SIZES[int(mt.group(1), 16)] = int(mt.group(2))
idx = json.load(open(PROJECT + '/Maps/world-index.json', encoding='utf-8'))


def load(mid):
    rel = idx[str(mid)].replace(chr(92), '/')
    base = os.path.splitext(os.path.basename(rel))[0]
    p = PROJECT + '/' + os.path.dirname(rel) + '/events/' + base + '.events.json'
    return base, json.load(open(p, encoding='utf-8'))


def entries(d):
    out = []
    for s in SLOTS:
        for i, e in enumerate(d.get('EventCodes%sTable' % s, [])):
            out.append((s, i, e))
    return out


def decode_from(codes, start):
    pc = start
    res = []
    while 0 <= pc < len(codes):
        op = codes[pc]
        sz = SIZES.get(op, 0)
        res.append((pc, op, bytes(codes[pc + 1:pc + max(sz, 1)])))
        if sz <= 0:
            break
        pc += sz
    return res


TARGET = {0xA9, 0xAA, 0xB2, 0xB4, 0xB5, 0xB6, 0xB7, 0x9E}

if __name__ == '__main__':
    found = {}
    nmaps = 0
    for mid in sorted(int(k) for k in idx):
        try:
            base, d = load(mid)
        except Exception as ex:
            print('LOAD FAIL', mid, ex)
            continue
        nmaps += 1
        codes = d['Codes']
        for s, i, e in entries(d):
            for pc, op, ops in decode_from(codes, e):
                if op in TARGET:
                    found.setdefault((mid, pc, op, ops.hex()), []).append((s, i))
    print('maps', nmaps, 'distinct (map,pc) sites', len(found))
    rows = []
    for (mid, pc, op, ops), owners in sorted(found.items()):
        rows.append((mid, pc, op, ops, sorted(set(owners))))
    for r in rows:
        print(r)
