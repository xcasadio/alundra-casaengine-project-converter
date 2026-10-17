#!/usr/bin/env python3
"""critic-complet c1: which test files of Alundra.Tests/ name a map that holds liftable records (kind != 0).

Read-only. Inputs: ../dll/all_records.json (census of the DLL discovery, kind/collidable per record), the exported tileMap
file names under alundra-project/Maps (world name -> map index), and every Alundra.Tests/*.cs file (text search).
A test file 'names' a map when it contains the world name suffix '-NNN"' (ArcSpec / world loader strings) or a
'MapIndex' / 'map' integer literal NNN next to a map keyword. The second rule is loose: its hits are listed separately.
Output: stdout.
"""
import glob, json, os, re, sys
from collections import defaultdict

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = 'D:/development/repo/alundra-casaengine-project-converter'
recs = json.load(open(os.path.join(HERE, '..', 'dll', 'all_records.json'), encoding='utf-8'))
lift = defaultdict(int)
for r in recs:
    if r['kind'] != 0:
        lift[r['map']] += 1
names = {}
for f in glob.glob(f'{REPO}/alundra-project/Maps/*/*/tilemap/*.tileMap'):
    b = os.path.basename(f)[:-len('.tileMap')]
    m = re.search(r'-(\d+)$', b)
    if m:
        names[int(m.group(1))] = b
tests = sorted(glob.glob(f'{REPO}/Alundra.Tests/*.cs'))
texts = {os.path.basename(t): open(t, encoding='utf-8', errors='replace').read() for t in tests}
strict = defaultdict(list)
loose = defaultdict(list)
for fn, tx in texts.items():
    for mp, n in lift.items():
        if re.search(r'-%d"' % mp, tx):
            strict[mp].append(fn)
        elif re.search(r'(MapIndex|mapIndex|Map|map)\s*[=:(,]\s*%d\b' % mp, tx) or re.search(r'\bMap%d\b' % mp, tx):
            loose[mp].append(fn)
print('maps with liftables: %d ; records %d' % (len(lift), sum(lift.values())))
print('== maps with liftables named by a world-name string "-NNN\\"" in a test file')
for mp in sorted(strict):
    print('  %4d (%3d liftables) %-50s : %s' % (mp, lift[mp], names.get(mp, '?'), ', '.join(sorted(strict[mp]))))
print('== loose hits only (integer literal next to a map keyword), to be read by hand')
for mp in sorted(loose):
    if mp in strict:
        continue
    print('  %4d (%3d liftables) %-50s : %s' % (mp, lift[mp], names.get(mp, '?'), ', '.join(sorted(loose[mp]))))
