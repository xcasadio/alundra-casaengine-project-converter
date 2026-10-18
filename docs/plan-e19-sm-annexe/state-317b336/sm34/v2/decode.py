#!/usr/bin/env python3
"""Read-only: linear decode of one program of a map (sizes of EventOpcodeSizeTable.cs at HEAD).
Usage: decode.py <map> <slot> <program> [maxinstr] [from_pc]"""
import json, os, re, sys

REPO = 'D:/development/repo/alundra-casaengine-project-converter'
index = json.load(open(REPO + '/alundra-project/Maps/world-index.json', encoding='utf-8-sig'))
sizes, names = {}, {}
for line in open(REPO + '/Alundra/Events/EventOpcodeSizeTable.cs', encoding='utf-8-sig'):
    m = re.search(r'\{ 0x([0-9A-F]{2}), new\((\d+), "([^"]*)"\) \}', line)
    if m:
        sizes[int(m.group(1), 16)] = int(m.group(2))
        names[int(m.group(1), 16)] = m.group(3)

m, slot, prog = int(sys.argv[1]), sys.argv[2], int(sys.argv[3])
maxi = int(sys.argv[4]) if len(sys.argv) > 4 else 40
rel = index[str(m)].replace('\\', '/')
folder = os.path.dirname(REPO + '/alundra-project/' + rel)
name = os.path.splitext(os.path.basename(rel))[0]
d = json.load(open(f'{folder}/events/{name}.events.json', encoding='utf-8-sig'))
codes = d['Codes']
pc = d[f'EventCodes{slot}Table'][prog]
if len(sys.argv) > 5:
    pc = int(sys.argv[5])
print(f'map {m} {slot}[{prog}] entry {d[f"EventCodes{slot}Table"][prog]}')
for _ in range(maxi):
    op = codes[pc]
    s = sizes.get(op, 0)
    b = ' '.join(f'{x:02X}' for x in codes[pc:pc + max(s, 1)])
    print(f'  @{pc}: {b}   {names.get(op, "?")}')
    if s <= 0 or op == 0xFF:
        break
    pc += s
