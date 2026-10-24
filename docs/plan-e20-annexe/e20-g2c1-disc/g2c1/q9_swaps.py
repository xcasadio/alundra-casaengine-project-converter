"""q9_swaps.py - placements of the liftable families (header C in {4, 23, 70}) whose scripts (A/C/D/E/F programs of the placement) execute opcode 0x41 (SpriteProgramIndexes[slot] = value) or 0x40 on slot C,
i.e. that change which native handler runs. READ-ONLY. Run: python -B -I -X utf8 q9_swaps.py"""
import collections, glob, json, re, sys
sys.path.insert(0, 'D:/development/repo/alundra-casaengine-project-converter/docs/plan-e20-annexe/e20-g3/dll')
import decode_events as de
R = r"D:/development/repo/alundra-casaengine-project-converter"
D = R + "/data-extracted/data"
SLOTS = (('A', 'EventCodesA_LoadIndex'), ('C', 'EventCodesC_TickIndex'), ('D', 'EventCodesD_TouchIndex'), ('E', 'EventCodesE_DeactivateIndex'), ('F', 'EventCodesF_InteractIndex'))
out = []
for f in sorted(p for p in glob.glob(D + '/map_*.json') if re.search(r'map_\d+\.json$', p)):
    n = int(re.search(r'map_(\d+)\.json', f).group(1))
    d = json.load(open(f, encoding='utf-8-sig'))
    si = d['SpriteInfo']; recs = si['SpriteRecords']
    doc = None
    ends = None
    for k, e in enumerate(si['Entities']['Entities']):
        if e is None or not e['IsEnabled']:
            continue
        i = e['SpriteTableIndex']
        r = recs[i] if i < len(recs) else None
        if r is None:
            continue
        h = r['Header']
        if h['ProgramTick'] not in (4, 23, 70) or not ((h['CanPickup'] >> 1) & 3):
            continue
        for sl, key in SLOTS:
            ii = e[key]
            if not ii:
                continue
            if doc is None:
                doc = de.load_map(n)
                ends = {(a, c): b for a, _, c, b in de.all_programs(doc)}
            tab = doc['EventCodes%sTable' % sl]
            s = tab[ii & 0x7f]
            en = ends.get((sl, s), len(doc['Codes']))
            for pc, op, v, bad in de.sweep(doc, s, en):
                if op == 0x41 and v:
                    out.append((i, h['ProgramTick'], n, k, sl, ii, 'SpriteProgramIndexes[%d] = %d' % (v[1], v[2])))
                if op == 0x40 and v and v[1] == 2:
                    out.append((i, h['ProgramTick'], n, k, sl, ii, 'ProgramIndexes[C] = %d' % v[2]))
cnt = collections.Counter((o[0], o[1], o[4], o[6]) for o in out)
for kk, c in sorted(cnt.items()):
    print('sprite %3d header C%-2d  in %s program: %-34s x %d' % (kk[0], kk[1], kk[2], kk[3], c))
print()
for o in out:
    if o[0] == 48 or '0x41' in o[6] or 'SpriteProgramIndexes' in o[6]:
        print('  sprite %3d C%d map %d rec %d slot %s idx %d : %s' % o)
