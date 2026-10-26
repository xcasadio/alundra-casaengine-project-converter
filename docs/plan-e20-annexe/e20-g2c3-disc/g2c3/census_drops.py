"""census_drops.py - read-only census of the water drops (map-page sprite 135, type 391 = 0x187, A19 + C71) and of the torches, per map, over data-extracted/data/map_*.json.
Lists for every placement: rec, enabled, x, y, height, direction, A/B/C/D/E/F program indices, contents; the header of the sprite record (programs A,B,C,D,E,F + body + flags);
then the C program of each drop (decoded with the repo's decode_events.py).  Run: python -B -I -X utf8 census_drops.py"""
import json, glob, re, collections, os, sys
R = r"D:/development/repo/alundra-casaengine-project-converter"
D = R + "/data-extracted/data"
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, R + '/docs/plan-e20-annexe/e20-g3/dll')
import decode_events as de
out = []
tot = collections.Counter()
per_map = collections.defaultdict(lambda: collections.defaultdict(list))
hdrs = {}
for f in sorted((p for p in glob.glob(D + '/map_*.json') if re.search(r'map_\d+\.json$', p)), key=lambda p: int(re.search(r'map_(\d+)\.json', p).group(1))):
    n = int(re.search(r'map_(\d+)\.json', f).group(1))
    d = json.load(open(f, encoding='utf-8-sig'))
    si = d['SpriteInfo']
    for k, e in enumerate(si['Entities']['Entities']):
        if e is None:
            continue
        i = e['SpriteTableIndex']
        if i not in (135, 137):
            continue
        r = si['SpriteRecords'][i] if i < len(si['SpriteRecords']) else None
        if r is None:
            continue
        h = r['Header']
        kind = None
        if i == 135 and h['ProgramTick'] == 71:
            kind = 'drop'
        elif i == 137 and h['ProgramTick'] == 70:
            kind = 'torch'
        if kind is None:
            continue
        hdrs.setdefault(kind, {})[n] = {k: v for k, v in h.items()}
        idx = dict(A=e['EventCodesA_LoadIndex'], B=e['EventCodesB_MapIndex'], C=e['EventCodesC_TickIndex'], D=e['EventCodesD_TouchIndex'], E=e['EventCodesE_DeactivateIndex'], F=e['EventCodesF_InteractIndex'])
        per_map[n][kind].append((k, e['IsEnabled'], e['XPos'], e['YPos'], e['Height'], e['SpriteDirection'], idx, e['Contents'], e['XMin'], e['XMax'], e['YMin'], e['YMax']))
        if e['IsEnabled']:
            tot[kind] += 1
out.append('TOTAL enabled placements: %s' % dict(tot))
out.append('')
# header equality across maps
for kind in ('drop', 'torch'):
    hs = hdrs.get(kind, {})
    ref = next(iter(hs.values()))
    same = all(v == ref for v in hs.values())
    out.append('header of the %s sprite identical on all %d maps: %s' % (kind, len(hs), same))
    out.append('   header (map %s): %s' % (next(iter(hs)), json.dumps(ref, sort_keys=True)))
    if not same:
        for m_, v in hs.items():
            if v != ref:
                out.append('   DIFF map %d: %s' % (m_, {k: (v[k], ref.get(k)) for k in v if v[k] != ref.get(k)}))
out.append('')
out.append('--- per map: counts')
for n in sorted(per_map):
    m = per_map[n]
    out.append('map %3d: drops %d (enabled %d), torches %d (enabled %d)' % (n, len(m.get('drop', [])), sum(1 for x in m.get('drop', []) if x[1]), len(m.get('torch', [])), sum(1 for x in m.get('torch', []) if x[1])))
out.append('')
out.append('--- drops (map rec en pos(x,y,h) dir idx contents zone)')
for n in sorted(per_map):
    for t in per_map[n].get('drop', []):
        out.append('map %3d rec %3d en=%d pos(%3d,%3d,h%3d) dir=%3d idx=%s contents=%d zone x[%d..%d] y[%d..%d]' % ((n,) + t))
# C programs of the drops
out.append('')
out.append('--- C programs of the drops (decoded)')
progs = collections.Counter()
for n in sorted(per_map):
    if not per_map[n].get('drop'):
        continue
    doc = de.load_map(n)
    allstarts = sorted({st for _, _, st in de.programs(doc)} | {len(doc['Codes'])})
    def byslot_get(slot, i):
        st = doc['EventCodes%sTable' % slot][i & 0x7F]
        nx = next((x for x in allstarts if x > st), len(doc['Codes']))
        return st, nx
    seen = {}
    for t in per_map[n]['drop']:
        c = t[6]['C']
        if not (c & 0x7F) or (n, c) in seen:
            continue
        s, e_ = byslot_get('C', c)
        steps = []
        for pc, op, v, bad in de.sweep(doc, s, e_):
            steps.append('0x%02X%s%s' % (op, list(v), ' BAD' if bad else ''))
            if bad:
                break
        seen[(n, c)] = steps
        progs[' '.join(steps)] += 1
        out.append('map %d C[%d] @%d..%d: %s' % (n, c, s, e_, ' '.join(steps)))
out.append('')
out.append('--- distinct C programs (count)')
for k, v in progs.most_common():
    out.append('%3d x %s' % (v, k))
txt = '\n'.join(out)
print(txt)
open(os.path.join(HERE, 'census_drops.txt'), 'w', encoding='utf-8').write(txt + '\n')
