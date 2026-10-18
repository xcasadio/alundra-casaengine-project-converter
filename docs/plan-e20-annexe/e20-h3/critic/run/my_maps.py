"""my_maps.py - critic's own corpus scan of data-extracted/data/map_*.json: bomb/beans givers (0x82), placements of global sprites 1/2, chest contents, opcode 0x87/0x95 users of attack index 3.
Opcode sizes parsed from Alundra/Events/EventOpcodeSizeTable.cs (not from the other agent's scripts)."""
import json, re, glob, collections, sys
SIZES = {}
for line in open(r"D:/development/repo/alundra-casaengine-project-converter/Alundra/Events/EventOpcodeSizeTable.cs", encoding='utf-8'):
    m = re.search(r'\{ 0x([0-9A-Fa-f]{2}), new\((\d+), "([^"]*)"\)', line)
    if m: SIZES[int(m.group(1), 16)] = (int(m.group(2)), m.group(3))
DATA = r"D:/development/repo/alundra-casaengine-project-converter/data-extracted/data/"
def programs(d):
    ec = d['SpriteInfo']['EventCodes']
    codes = ec['Codes']
    tabs = {}
    for k, nm in (('EventCodesATable', 'A'), ('EventCodesBTable', 'B'), ('EventCodesCTable', 'C'), ('EventCodesDTable', 'D'), ('EventCodesETable', 'E'), ('EventCodesFTable', 'F')):
        tabs[nm] = ec[k]
    offs = sorted({o for t in tabs.values() for o in t if o > 0})
    return codes, tabs, offs
def decode(codes, start, end):
    out = []
    p = start
    end = min(end, len(codes))
    while p < end:
        op = codes[p]
        sz = SIZES.get(op, (0, ''))[0]
        if sz < 1:
            out.append((p, op, None, 'UNKNOWN')); break
        if p + sz > len(codes): break
        out.append((p, op, codes[p:p+sz], SIZES[op][1]))
        p += sz
    return out
def main():
    found = collections.defaultdict(list)
    maps = {}
    for f in glob.glob(DATA + 'map_*.json'):
        m = re.search(r'map_(\d+)\.json$', f)
        if not m: continue
        mid = int(m.group(1))
        try: d = json.load(open(f, encoding='utf-8-sig'))
        except Exception: continue
        if 'SpriteInfo' not in d: continue
        maps[mid] = d
    print('maps loaded', len(maps))
    for mid in sorted(maps):
        d = maps[mid]
        codes, tabs, offs = programs(d)
        ents = d['SpriteInfo']['Entities']['Entities']
        for i, e in enumerate(ents):
            if e is None: continue
            isglob = (e['SpriteDirection'] & 0x80) == 0     # bit 7 of the record byte +5 = map page (0x8003A260-0x8003A27C); clear = global page
            if isglob and e['SpriteTableIndex'] in (1, 2):
                found['placement_sprite%d' % e['SpriteTableIndex']].append((mid, i, e['IsEnabled'], e['XPos'], e['YPos'], e['EventCodesC_TickIndex'], e['EventCodesD_TouchIndex'], e['EventCodesE_DeactivateIndex']))
            if e["Contents"] in (0x23, 0x1F) and isglob:
                found['contents_%02x' % e['Contents']].append((mid, i, e['SpriteTableIndex'], e['IsEnabled']))
        # program decode: each table entry start offset -> next offset
        for nm, t in tabs.items():
            for pi, off in enumerate(t):
                if off <= 0 or off >= len(codes): continue
                nxt = [o for o in offs if o > off]
                end = nxt[0] if nxt else len(codes)
                for (p, op, raw, name) in decode(codes, off, end):
                    if raw is None: break
                    if op == 0x82 and raw[1] in (0x1F, 0x23):
                        found['give_%02x' % raw[1]].append((mid, nm, pi, p))
                    if op == 0x87 and raw[2] == 3:
                        found['0x87_idx3'].append((mid, nm, pi, p))
                    if op == 0x95 and raw[9] == 3:
                        found['0x95_idx3'].append((mid, nm, pi, p, raw[7] | raw[8] << 8))
    for k in sorted(found):
        v = found[k]
        mapset = sorted({x[0] for x in v})
        print('%-22s %3d sites in %3d maps: %s' % (k, len(v), len(mapset), mapset if len(mapset) < 40 else str(mapset[:40]) + '...'))
    import collections as C
    by = C.defaultdict(list)
    for x in found['0x95_idx3']: by[x[4]].append(x[0])
    print('0x95 type-3 sites by mask:', {hex(k): (len(v), sorted(set(v))) for k, v in by.items()})
    for k in ('give_23', 'give_1f', 'placement_sprite1', 'placement_sprite2', 'contents_23', 'contents_1f'):
        print(k, found.get(k))
if __name__ == '__main__':
    main()
