"""q2_logs.py - Q-G2C-2: the scripted placements of the C4 family (sprites 34, 37, 107, 111, 112, 113, 117): which programs A/C they carry,
what the programs do (full linear sweep from the table start to the next program start), whether any hands control back to the native C4
(opcode 0x40 with slot 2 value 0, or 0x41 changing the sprite program index).
READ-ONLY.  Run: python -B -I -X utf8 q2_logs.py [sprite ...]   (default 117)"""
import collections, glob, json, os, re, sys
sys.path.insert(0, 'D:/development/repo/alundra-casaengine-project-converter/docs/plan-e20-annexe/e20-g3/dll')
import decode_events as de

R = r"D:/development/repo/alundra-casaengine-project-converter"
D = R + "/data-extracted/data"
want = [int(a) for a in sys.argv[1:]] or [117]
SLOTS = (('A', 'EventCodesA_LoadIndex'), ('B', 'EventCodesB_MapIndex'), ('C', 'EventCodesC_TickIndex'), ('D', 'EventCodesD_TouchIndex'),
         ('E', 'EventCodesE_DeactivateIndex'), ('F', 'EventCodesF_InteractIndex'))
rows = []
for f in sorted(p for p in glob.glob(D + '/map_*.json') if re.search(r'map_\d+\.json$', p)):
    n = int(re.search(r'map_(\d+)\.json', f).group(1))
    d = json.load(open(f, encoding='utf-8-sig'))
    si = d['SpriteInfo']; recs = si['SpriteRecords']
    for k, e in enumerate(si['Entities']['Entities']):
        if e is None or not e['IsEnabled']:
            continue
        if e['SpriteTableIndex'] not in want:
            continue
        r = recs[e['SpriteTableIndex']] if e['SpriteTableIndex'] < len(recs) else None
        if r is None or r['Header']['ProgramTick'] != 4:
            continue
        idx = {s: e[key] for s, key in SLOTS}
        if idx['A'] or idx['C'] or idx['D'] or idx['E'] or idx['F']:
            rows.append((n, k, e['SpriteTableIndex'], idx, (e['XPos'], e['YPos'], e['Height']), e['SpriteDirection'], (e['XMin'], e['XMax'], e['YMin'], e['YMax'])))
print('scripted placements:', len(rows), 'by sprite', dict(collections.Counter(r[2] for r in rows)))
bymap = collections.defaultdict(list)
for r in rows:
    bymap[r[0]].append(r)
for n in sorted(bymap):
    print('map', n, 'count', len(bymap[n]))
    for (n_, k, sp, idx, pos, dr, zone) in bymap[n]:
        print('   rec %3d sprite %d idx A%d C%d D%d E%d F%d pos=%s dir=%d zone=%s' % (k, sp, idx['A'], idx['C'], idx['D'], idx['E'], idx['F'], pos, dr, zone))
# program dump per (map, slot, idx)
seen = set()
summary = {}
for n in sorted(bymap):
    doc = de.load_map(n)
    if doc is None:
        print('map', n, 'NO EVENTS FILE'); continue
    progs = {(s, i): (a, b) for s, i, a, b in [(sl, ii, ss, ee) for sl, ii, ss, ee in de.all_programs(doc)]}
    # index -> (start,end) by table
    ends = {}
    for sl, i, s, e in de.all_programs(doc):
        ends[(sl, s)] = e
    for (n_, k, sp, idx, pos, dr, zone) in bymap[n]:
        for sl in 'ACDEF':
            ii = idx[sl]
            if not ii or (n, sl, ii) in seen:
                continue
            seen.add((n, sl, ii))
            tab = doc['EventCodes%sTable' % sl]
            if (ii & 0x7f) >= len(tab):
                print('== map %d %s[%d] OUT OF TABLE' % (n, sl, ii)); continue
            s = tab[ii & 0x7f]
            e = ends.get((sl, s), len(doc['Codes']))
            ins = de.sweep(doc, s, e)
            ops = [(pc, op, list(v) if v else None) for pc, op, v, bad in ins]
            sig = tuple(op for _, op, _ in ops)
            has40 = [(pc, v) for pc, op, v in ops if op == 0x40]
            has41 = [(pc, v) for pc, op, v in ops if op == 0x41]
            back = [(pc, v) for pc, v in has40 if v and v[1] == 2 and v[2] == 0]
            summary[(n, sl, ii)] = dict(start=s, end=e, nops=len(ops), has40=has40, has41=has41, back=back, sig=sig)
            print('== map %d %s[%d] @%d..%d  ops=%d  0x40=%s 0x41=%s native-return(40 02 00)=%s' % (n, sl, ii, s, e, len(ops), has40, has41, bool(back)))
            for pc, op, v in ops[:60]:
                print('     @%d 0x%02X %s' % (pc, op, v))
print()
print('programs total', len(summary), 'with a return to the native C4 (40 02 00):', sum(1 for v in summary.values() if v['back']))
c = collections.Counter(v['sig'] for k, v in summary.items() if k[1] == 'C')
print('distinct C op-signatures:', len(c))
for sig, cnt in c.most_common(10):
    print('  x%d' % cnt, ' '.join('%02X' % o for o in sig[:40]))
