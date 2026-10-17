#!/usr/bin/env python3
"""critic-complet c6: the liftable records (kind != 0, from ../dll/all_records.json) split by the map-load spawn gate of the DLL
(EntitySpawnFactory.ShouldSpawnRecord, EntitySpawnFactory.cs:84-99: IsEnabled == 0 -> never; SpriteDirection & 0x40 == 0 -> not at the
map load, only by 0x2D / SpawnEntityByRecordId). SpriteDirection and IsEnabled are read from the exported tileMap 'Entities' layer
(same files as the census). Read-only, stdout utf-8. Usage: c6_spawn_gate.py [MAP ...]  (no map = all; chain maps summarised)."""
import glob, json, os, re, sys
from collections import defaultdict
sys.stdout.reconfigure(encoding='utf-8')
HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = 'D:/development/repo/alundra-casaengine-project-converter/alundra-project'
CHAIN = [389, 390, 476, 478, 392, 391, 416, 163, 162, 165, 164, 172, 169, 170, 171, 173, 174, 175, 10, 179, 176, 177, 178, 180, 181,
         182, 135, 183, 184, 185]
recs = json.load(open(os.path.join(HERE, '..', 'dll', 'all_records.json'), encoding='utf-8'))
lift = {(r['map'], r['idx']): r for r in recs if r['kind']}
props = {}
for f in glob.glob(f'{ROOT}/Maps/*/*/tilemap/*.tileMap'):
    m = re.search(r'-(\d+)\.tileMap$', os.path.basename(f))
    if not m:
        continue
    mp = int(m.group(1))
    d = json.load(open(f, encoding='utf-8'))
    for l in d['object_layers']:
        if l['name'] == 'Entities':
            for o in l['objects']:
                p = o['custom_properties']
                props[(mp, int(p['Index']))] = (int(p.get('IsEnabled', 1)), int(p.get('SpriteDirection', 0)))
cnt = defaultdict(int)
per_map = defaultdict(lambda: defaultdict(int))
script_only = []
for k, r in lift.items():
    en, sd = props.get(k, (None, None))
    if en is None:
        cls = 'missing'
    elif en == 0:
        cls = 'disabled'
    elif sd & 0x40 == 0:
        cls = 'script-only (0x40 clear)'
        script_only.append((k[0], k[1], r['name'], sd))
    else:
        cls = 'spawned at load'
    cnt[cls] += 1
    per_map[k[0]][cls] += 1
print('liftable records: %d' % len(lift))
for c, n in sorted(cnt.items()):
    print('  %-26s %d' % (c, n))
print('chain maps:')
for mp in CHAIN:
    if mp in per_map:
        print('  %d %s' % (mp, dict(per_map[mp])))
sel = [int(a) for a in sys.argv[1:]]
print('script-only liftables%s:' % (' on ' + str(sel) if sel else ' (first 60)'))
rows = [s for s in script_only if not sel or s[0] in sel]
for s in rows[: (None if sel else 60)]:
    print('  map %d rec%d %s SpriteDirection=%d' % s)
