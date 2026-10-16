"""p8_loadmap.py - CRITIC probe V8: the REAL map-load record loop (0x8003B2EC-0x8003B354) on the MIPS machine, for a whole map.
Both sprite pages are installed (espawn.World), the 128 entity records of the map are written in the map block (+8 = record array, +0x14 = 128), the hero tile (+0x120/+0x124 of slot 0)
is set, and the loop is reproduced exactly: stop at the first record whose first word is 0, else call the REAL spawner 0x8003A1B8(0, i, 0) for i = 0..127.
Output per map: records called, entities created (slot order), loot item / +0x40 of each, draws, final RNG word.  Compared with p4_loadsim.py (zone ignored) to measure the window.
usage: python -B p8_loadmap.py MAP [HEROTILEX HEROTILEY]"""
import sys, os, json
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, 'tools'))
sys.path.insert(0, HERE)
import espawn as X
from machine import M32, s32

ENT_REC = 0x801CF000
MT = X.MT
RNG = X.RNG


def rec_bytes(e):
    if e is None:
        return bytes(20)
    return bytes([e['XMin'], e['YMin'], e['XMax'], e['YMax'], e['IsEnabled'], e['SpriteDirection'], e['SpriteTableIndex'], e['XPos'], e['YPos'], e['Height'],
                  e['EventCodesA_LoadIndex'] & 0xFF, e['EventCodesB_MapIndex'] & 0xFF, e['EventCodesC_TickIndex'] & 0xFF, e['EventCodesD_TouchIndex'] & 0xFF,
                  e['EventCodesE_DeactivateIndex'] & 0xFF, e['EventCodesF_InteractIndex'] & 0xFF]) + (e['_10'] & 0xFFFF).to_bytes(2, 'little') + (e['Contents'] & 0xFFFF).to_bytes(2, 'little')


def lcg(s):
    return (s * 0x7D2B89DD + 0xE06A02E7) & M32


def load_map(map_no, tile=None, seed=0xB017C93D):
    w = X.World(map_no=map_no, seed=seed)
    m = w.m
    ents = X.mapjson(map_no)['Entities']['Entities']
    blob = b''.join(rec_bytes(e) for e in ents)
    assert len(blob) == 128 * 20
    m.poke_bytes(ENT_REC, blob)
    m.poke(MT + 8, ENT_REC)
    m.poke(MT + 0x14, 128)
    hero = X.ent(0)
    if tile is None:
        e0 = next(e for e in ents if e)
        tile = ((e0['XMin'] + e0['XMax']) // 2, (e0['YMin'] + e0['YMax']) // 2)
    m.poke(hero + 0x120, tile[0])
    m.poke(hero + 0x124, tile[1])
    m.poke(RNG, seed)
    out = []
    for i in range(128):
        first = m.peek(ENT_REC + 20 * i)
        if first == 0:
            out.append(dict(i=i, stop=True))
            break
        before = m.peek(RNG)
        p = m.call(0x8003A1B8, 0, i, 0, limit=5_000_000) & M32
        after = m.peek(RNG)
        n = 0
        t = before
        while t != after and n < 6:
            t = lcg(t)
            n += 1
        row = dict(i=i, ptr=p, draws=n if t == after else -1)
        if p:
            row.update(slot=(p - X.ENT0) // X.STRIDE, item=m.peek(p + 0x3C), flag=m.peek(p + 0x40), sprite=m.peek(p + 0x68))
        out.append(row)
    return tile, out, m.peek(RNG), w.printed


if __name__ == '__main__':
    mp = int(sys.argv[1])
    tile = (int(sys.argv[2]), int(sys.argv[3])) if len(sys.argv) > 3 else None
    tile, rows, final, printed = load_map(mp, tile)
    spawned = [r for r in rows if r.get('ptr')]
    refused = [r for r in rows if 'ptr' in r and not r['ptr']]
    draws = sum(r['draws'] for r in rows if 'draws' in r)
    print('map %d hero tile %s: records called %d, entities created %d, refused %d, load draws %d, final word %08X, printer calls %d'
          % (mp, tile, sum(1 for r in rows if 'ptr' in r), len(spawned), len(refused), draws, final, len(printed)))
    print('  rolling entities (item, flag, draws):', [(r['i'], r['item'], r['draws']) for r in spawned if r['draws']][:30])
