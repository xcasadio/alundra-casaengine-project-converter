"""mapping_scan.py - E20.h4c2 T0 (b): the distinct (address, size) pairs read and written by the vectors of E13, of the camera and of the sway, and the vectors that read a non-zero camera kick X.
usage: python -I mapping_scan.py <e20-h4b/bin/vec dir>"""
import json
import sys
from collections import Counter

V = sys.argv[1]
for f in ('vec_e13_8007F30C.json', 'vec_camera_8002CDA0.json', 'vec_sway_8002C894.json'):
    d = json.load(open(V + '/' + f))['vectors']
    rd, wr = Counter(), Counter()
    for v in d:
        base = v['args'][0] if f.startswith('vec_e13') else None
        for a, n, val in v['reads']:
            rd[(a - base if base is not None and 0 <= a - base < 0x300 else a, n)] += 1
        for a, n, val in v['writes']:
            wr[(a - base if base is not None and 0 <= a - base < 0x300 else a, n)] += 1
    print('==', f, len(d), 'vectors')
    print('  reads ', sorted((hex(a), n, c) for (a, n), c in rd.items()))
    print('  writes', sorted((hex(a), n, c) for (a, n), c in wr.items()))
d = json.load(open(V + '/vec_camera_8002CDA0.json'))['vectors']
ids = []
for v in d:
    kx = [val for a, n, val in v['reads'] if a == 0x800dd870]
    if kx and kx[0] != 0:
        ids.append((v['id'], kx[0] if kx[0] < 2 ** 31 else kx[0] - 2 ** 32))
print('vectors reading a non-zero camera kick X [0x800DD870]:', len(ids), ids)
