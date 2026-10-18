"""scen_beans.py - critic: beans thrown (Circle then Square) with and without a REAL magic pot (map sprite 153 record of map 12 installed on the map page)."""
import json, sys
import spellworld as S
import world as W
import bank
from scen_lib import *
from scen_zone import spawn
MAPJSON = r"D:/development/repo/alundra-casaengine-project-converter/data-extracted/data/map_12.json"
MAPPAGE = 0x80126E70

def make_world(with_pot, px=None, py=100.0, pz=0.0, indices=(1, 2)):
    m, STATS, placed = S.make(item=0x1F, indices=indices)
    pot = None
    if with_pot:
        d = json.load(open(MAPJSON, encoding='utf-8-sig'))
        rec = d['SpriteInfo']['SpriteRecords'][153]
        base = 0x801C8000
        info = bank.install(m, base, base, rec)
        PT = 0x801EB000
        m.zero(PT, 256 * 4)
        m.poke(PT + 4 * 153, base)
        m.poke(MAPPAGE + 0xC, PT)
        m.poke(MAPPAGE + 0x10, 256)
        pot = spawn(m, W.ent(0), 1, 153, px, py, pz)
    return m, STATS, pot

def go(with_pot, px=None, nticks=130, label=''):
    m, STATS, pot = make_world(with_pot, px)
    log = run(m, nticks, lambda t: (W.CIRCLE if t == 1 else (W.SQUARE if t == 42 else 0)))
    prev = None
    print('=== beans', label, 'pot at', pot and hex(pot), 'x', px)
    for r in log:
        beans = {i: (e['st'], e['tanim'], e['canim'], round(e['x'], 1), round(e['z'], 1), e['end'], hex(m.peek(W.ent(i) + 0x12C))) for i, e in r['ents'].items() if e['sp'] == 1}
        pots = {i: (e['st'], e['tanim'], e['canim'], round(e['z'], 1)) for i, e in r['ents'].items() if e['sp'] == 0x199}
        key = (tuple(beans.items()), tuple(pots.items()), r['jr'] and tuple((x[1], x[2] if x[1] == 'sound' else x[2][:2]) for x in r['jr']))
        if key != prev and r['t'] > 38:
            print(' t%3d' % r['t'], 'beans', beans, 'pot', pots, [(x[1], x[2] if x[1] == 'sound' else x[2][:2]) for x in r['jr']][:5])
        prev = key
if __name__ == '__main__':
    go(False, label='no pot', nticks=100)
