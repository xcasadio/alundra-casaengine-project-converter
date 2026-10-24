import sys, os, collections, json
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import evlib as L
CHAIN = {389, 390, 476, 478, 392, 391, 416, 163, 162, 165, 164, 172, 169, 170, 171, 173, 174, 175, 10, 179, 176, 177, 178, 180, 181, 182, 135, 183, 184, 185}
WATCH = {4, 23, 70}
out = []
for m in L.all_map_ids():
    doc = L.load_events(m)
    if not doc: continue
    d = L.load_map(m); si = d.get('SpriteInfo')
    ents = si['Entities']['Entities'] if si else []
    seen = set()
    for X in 'ABCDEF':
        tab = doc['EventCodes%sTable' % X]
        for i, s in enumerate(tab):
            if (X, s) in seen: continue
            seen.add((X, s))
            e = L.prog_end(doc, s)
            for pc, op, v, bad in L.sweep(doc, s, e):
                if op == 0x41 and v and v[1] == 2 and v[2] in WATCH:
                    # who references this program
                    users = []
                    xk = {'A':'EventCodesA_LoadIndex','B':'EventCodesB_MapIndex','C':'EventCodesC_TickIndex','D':'EventCodesD_TouchIndex','E':'EventCodesE_DeactivateIndex','F':'EventCodesF_InteractIndex'}[X]
                    for k, en in enumerate(ents):
                        if en and (en[xk] & 0x7f) == i and i != 0:
                            glob_ = (en['SpriteDirection'] & 0x80) == 0
                            users.append((k, ('G' if glob_ else 'M') + str(en['SpriteTableIndex'])))
                    out.append((m, m in CHAIN, X, i, pc, v[2], users[:6], len(users)))
c = collections.Counter()
for r in out:
    print(r); c[(r[1])] += 1
print(dict(c))
