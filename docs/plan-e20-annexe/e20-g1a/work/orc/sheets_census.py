"""sheets_census.py - REAL record spawner (0x8003A1B8) for one example of each liftable family; reads the balance sheet the binary attaches (+0x1C4): AttackCount (byte +0xE), the index in BALANCE.BIN, and the header-derived flags.
Run: python -B -I sheets_census.py"""
import os, sys, json, re, glob
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import gl
import k0lib as K
from machine import M32

R = "D:/development/repo/alundra-casaengine-project-converter"
BAL = json.load(open(R + "/alundra-project/Data/balance.json", encoding='utf-8-sig'))['EntriesBySpriteIndex']
# one (map, sprite index) per family, found by the census
FAMS = {}
for f in sorted(p for p in glob.glob(R + '/data-extracted/data/map_*.json') if re.search(r'map_\d+\.json$', p)):
    n = int(re.search(r'map_(\d+)\.json', f).group(1))
    d = json.load(open(f, encoding='utf-8-sig'))
    si = d['SpriteInfo']; recs = si['SpriteRecords']
    for i, r in enumerate(recs):
        if r is None: continue
        h = r['Header']
        if (h['CanPickup'] >> 1) & 3:
            k = (h['ProgramTick'], h['ProgramDeactivate'], h['MoreFlags'], h['BreakEffect'], h['Contents'], h['SizeX'], h['SizeY'], h['SizeZ'])
            if k not in FAMS: FAMS[k] = []
            if len(FAMS[k]) < 2 and any(e and e['SpriteTableIndex'] == i for e in si['Entities']['Entities']):
                FAMS[k].append((n, i))
out = []
blob = 0x80143DF8
for k, lst in sorted(FAMS.items()):
    for (n, i) in lst[:1]:
        m = gl.world(hero_px=(100, 100), direction=24)
        info = json.load(open(R + '/data-extracted/data/map_%d.json' % n, encoding='utf-8-sig'))['Info']
        gl.W.load_balance(m, info['D']); m.poke8(gl.W.MAPHDR + 0xB, info['D']); m.poke8(gl.W.MAPHDR + 9, info['BalanceLevel'])
        key = ('map_%d' % n, i)
        if key not in K._CACHE:
            K._CACHE[key] = K._record('map_%d' % n, i)
        rec_, ents_, ts_ = K._CACHE[key]
        K._CACHE[key] = (rec_, [dict(ents_[0], XMin=0, YMin=0, XMax=255, YMax=255)] + ents_[1:], ts_)    # harness: whole-map spawn zone (the real zone test needs the hero's tile inside)
        try:
            slots, info = K.spawn_record_entities(m, 'map_%d' % n, i, [(10, 11, 0)], rec_addr=0x801F0000, ent_addr=0x801F8000, ptr_addr=0x801F9000)
        except RuntimeError as ex:
            out.append('family C=%d E=%d MoreFlags=0x%02X Break=%d Contents=%d: REFUSED by the real record spawner (map %d record %d): %s' % (k[0], k[1], k[2], k[3], k[4], n, i, ex))
            continue
        e = K.ent(slots[0])
        sheet = m.peek(e + 0x1C4)
        off = sheet - blob
        idx = None
        for j in range(512):
            if (m.peek(blob + 2 * j) & 0xFFFF) == off:
                idx = j; break
        ac = m.peek(sheet + 0xC) >> 16 & 0xFF if False else (m.peek(sheet + 0xC) >> 16) & 0xFF
        flags = m.peek(e + 0x6C)
        b = BAL[idx] if idx is not None else None
        out.append('family C=%d E=%d MoreFlags=0x%02X Break=%d Contents=%d size=(%d,%d,%d)  map %d record %d: flags6C=0x%X kind=%d  sheet idx=%s AttackCount(byte 0xE)=%d  json AttackCount=%s attacks=%s MaxHp=%s' % (
            k[0], k[1], k[2], k[3], k[4], k[5], k[6], k[7], n, i, flags, (flags & 0x600) >> 9, idx, (m.peek(sheet + 0xC) >> 16) & 0xFF, b and b['AttackCount'], b and [(a['AttackType'], a['AttackAttribute'], a['Power']) for a in (b['Attacks'] or [])[:b['AttackCount']]], b and b['MaxHp']))
print('\n'.join(out))
open(os.path.join(HERE, 'out', 'sheets-census.txt'), 'w').write('\n'.join(out) + '\n')
