import sys, os, json, collections
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import evlib as L
TRUNK = 117
MAPS = [70,72,73,79,140,158]
tot = collections.Counter()
for m in MAPS:
    d = L.load_map(m); si = d['SpriteInfo']; recs = si['SpriteRecords']
    doc = L.load_events(m)
    print('=== map', m, 'recs', len(si['Entities']['Entities']))
    cprogs = collections.Counter(); aprogs = collections.Counter()
    for k, e in enumerate(si['Entities']['Entities']):
        if e is None or e['SpriteTableIndex'] != TRUNK: continue
        c = e['EventCodesC_TickIndex']; a = e['EventCodesA_LoadIndex']
        tot['all'] += 1
        if (c & 0x7f) != 0:
            tot['scripted'] += 1
        else:
            tot['native'] += 1
        cprogs[(c & 0x7f)] += 1; aprogs[(a & 0x7f)] += 1
        print('  rec %3d en=%d pos(%d,%d,h%d) A=%d C=%d D=%d E=%d F=%d B=%d dir=%d' % (k, e['IsEnabled'], e['XPos'], e['YPos'], e['Height'], a, c, e['EventCodesD_TouchIndex'], e['EventCodesE_DeactivateIndex'], e['EventCodesF_InteractIndex'], e['EventCodesB_MapIndex'], e['SpriteDirection']))
    print('  C programs used', dict(cprogs), 'A programs used', dict(aprogs))
    # all 0x40 and 0x41 / 0x42 / 0x43 anywhere in the map's code
    starts = sorted({s for X in 'ABCDEF' for s in doc['EventCodes%sTable' % X]})
    seen=set()
    for X in 'ABCDEF':
        for i, s in enumerate(doc['EventCodes%sTable' % X]):
            if (X, s) in seen: continue
            seen.add((X, s))
            e = L.prog_end(doc, s)
            for pc, op, v, bad in L.sweep(doc, s, e):
                if op in (0x40, 0x41, 0x42, 0x43):
                    print('   %s[%d]@%d op 0x%02X %s' % (X, i, pc, op, list(v[1:]) if v else ''))
tot_ = dict(tot); print(tot_)
