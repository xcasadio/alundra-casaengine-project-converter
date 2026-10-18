#!/usr/bin/env python3
"""Read-only: re-check every sm3/sm4 site of skipped-sites.tsv against the exported events of alundra-project.
Prints mismatches, per-opcode counts, and the program entry and previous instructions for the arc sites."""
import json, os, sys

REPO = 'D:/development/repo/alundra-casaengine-project-converter'
TSV = REPO + '/docs/plan-e19-sm-annexe/skipped-sites.tsv'
IDX = REPO + '/alundra-project/Maps/world-index.json'
SM3 = {'A9', 'AA', 'B2', 'B4', 'B5', 'B6', 'B7', '9E'}
SM4 = {'72', '88', '96', '97', 'B3', '15', '66', '7A'}

index = json.load(open(IDX, encoding='utf-8-sig'))
cache = {}

def events(m):
    if m not in cache:
        rel = index[str(m)].replace('\\', '/')
        folder = os.path.dirname(REPO + '/alundra-project/' + rel)
        name = os.path.splitext(os.path.basename(rel))[0]
        cache[m] = json.load(open(f'{folder}/events/{name}.events.json', encoding='utf-8-sig'))
    return cache[m]

rows = [l.rstrip('\n').split('\t') for l in open(TSV, encoding='utf-8')][1:]
bad = 0
count = {'sm3': 0, 'sm4': 0}
for r in rows:
    m, mname, slot, prog, pc, op, opnds, nxt, cons = r
    if op not in SM3 | SM4:
        continue
    count['sm3' if op in SM3 else 'sm4'] += 1
    d = events(int(m))
    codes = d['Codes']
    pc = int(pc)
    got = codes[pc]
    exp = [int(op, 16)] + [int(x, 16) for x in opnds.split()] if opnds else [int(op, 16)]
    if codes[pc:pc + len(exp)] != exp:
        bad += 1
        print('MISMATCH', r, codes[pc:pc + len(exp)])
    table = d[f'EventCodes{slot}Table']
    entry = table[int(prog)] if int(prog) < len(table) else None
    if entry is None or entry > pc:
        bad += 1
        print('ENTRY?', r, entry)
print('checked', count, 'bad', bad)
