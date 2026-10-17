#!/usr/bin/env python3
"""critic-complet c3: liftable placements (kind != 0) grouped by prefab name and bank, with map count. Read-only, stdout (utf-8)."""
import json, os, sys
from collections import defaultdict
sys.stdout.reconfigure(encoding='utf-8')
HERE = os.path.dirname(os.path.abspath(__file__))
recs = json.load(open(os.path.join(HERE, '..', 'dll', 'all_records.json'), encoding='utf-8'))
by = defaultdict(lambda: [0, set(), set(), set()])
for r in recs:
    if r['kind']:
        k = (r['name'], r['bank'], r['kind'], r['collidable'], r['flags'] & 0xFF)
        by[k][0] += 1; by[k][1].add(r['map'])
for k, v in sorted(by.items(), key=lambda kv: -kv[1][0]):
    print('%5d placements on %3d maps : %-34s bank=%-12s kind=%d coll=%d more=0x%02X maps=%s' % (v[0], len(v[1]), k[0], k[1], k[2], k[3], k[4], sorted(v[1])[:40]))
