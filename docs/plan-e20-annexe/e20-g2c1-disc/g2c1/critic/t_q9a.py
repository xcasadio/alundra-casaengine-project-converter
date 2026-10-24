import sys, os, glob, re, json, collections
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import evlib as L
FAM = {34:'tonneau',37:'pierre',107:'etoile',111:'lune',112:'eau',113:'soleil',117:'tronc'}
res = []
for m in L.all_map_ids():
    f = L.ROOT + '/data-extracted/data/map_%d.json' % m
    d = json.load(open(f, encoding='utf-8-sig')); si = d.get('SpriteInfo')
    if not si: continue
    doc = None
    for k, e in enumerate(si['Entities']['Entities']):
        if e is None or e['SpriteTableIndex'] not in FAM: continue
        if doc is None: doc = L.load_events(m)
        if doc is None: continue
        for X, key in (('A','EventCodesA_LoadIndex'),('C','EventCodesC_TickIndex'),('D','EventCodesD_TouchIndex'),('E','EventCodesE_DeactivateIndex')):
            idx = e[key] & 0x7f
            if idx == 0: continue
            s = doc['EventCodes%sTable' % X][idx] if idx < len(doc['EventCodes%sTable' % X]) else None
            if s is None: continue
            end = L.prog_end(doc, s)
            ops = [(pc, op, list(v[1:])) for pc, op, v, bad in L.sweep(doc, s, end) if v and op in (0x40, 0x41, 0x42, 0x43)]
            if ops:
                res.append((m, k, FAM[e['SpriteTableIndex']], X, idx, ops))
for r in res: print(r)
