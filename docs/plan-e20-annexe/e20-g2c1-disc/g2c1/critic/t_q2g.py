import sys, os, collections
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import evlib as L
MAPS = [72,73,79,140,158]
res = collections.Counter()
for m in MAPS:
    d = L.load_map(m); si = d['SpriteInfo']; doc = L.load_events(m)
    seenprog = {}
    for k, e in enumerate(si['Entities']['Entities']):
        if e is None or e['SpriteTableIndex'] != 117 or (e['SpriteDirection'] & 0x80) == 0: continue
        if (e['EventCodesC_TickIndex'] & 0x7f) == 0: continue
        flags = 0x2323E4
        ops = []
        for X, key in (('A','EventCodesA_LoadIndex'),('C','EventCodesC_TickIndex')):
            idx = e[key] & 0x7f
            if idx == 0: continue
            s = doc['EventCodes%sTable' % X][idx]; end = L.prog_end(doc, s)
            for pc, op, v, bad in L.sweep(doc, s, end):
                if op in (0x62, 0x63) and v:
                    mask = v[2] | (v[3] << 8)
                    ops.append((X, idx, pc, 'set' if op == 0x62 else 'clr', v[1], hex(mask)))
                    if v[1] == 128:
                        flags = (flags | mask) if op == 0x62 else (flags & ~mask)
        res[(m, hex(flags), bool(flags & 0x80))] += 1
        if k in (11, 25, 26): print(m, k, ops, hex(flags))
for k, v in sorted(res.items()): print(k, v)
