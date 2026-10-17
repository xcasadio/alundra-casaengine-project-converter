#!/usr/bin/env python3
"""Folder and file names of the exported maps with a liftable (chain maps). Read-only."""
import glob, os, re, json

ROOT = 'D:/development/repo/alundra-casaengine-project-converter/alundra-project'
LIFT = [390, 392, 163, 165, 164, 172, 170, 171, 173, 174, 175, 179, 177, 178, 180, 181, 182, 184]
names = {}
for f in glob.glob(f'{ROOT}/Maps/*/*/tilemap/*.tileMap'):
    f = f.replace(chr(92), '/')
    m = re.search(r'-(\d+)\.tileMap$', os.path.basename(f))
    if m and int(m.group(1)) in LIFT:
        names[int(m.group(1))] = (f.split('/Maps/')[1].split('/')[0], os.path.basename(f)[:-8])
for k in LIFT:
    print(k, names[k])
json.dump(names, open('lift_chain_map_names.json', 'w'))
