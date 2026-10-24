"""sc_damage_table.py - the damage of the bomb explosion (attack attribute 4, power 5, read from the bomb's own balance sheet by the REAL ComputeNewHp 0x8003A374) on every balance sheet of BALANCE.BIN
(512 indices, REAL 0x80044550 builds the sheet pointer), HP 1000 so that nothing dies on an overkill ; and the same for the beans' (no attack: AttackCount 0) and for the hero's own sheet (index 0).
usage: python -B sc_damage_table.py"""
import json
import collections
import h3_game as G
from h3_game import *


def main():
    m, STATS = G.make(0x23, hero_px=(200, 200))
    pad = W.Pad()
    m.tick_no = 1
    W.tick(m, pad, W.CIRCLE, check_pad=False)              # the bomb is born (carried): its sheet and its current attack pointer (+0x1C8) are set by the real spawner
    bomb = W.ent(1)
    tp = G.add_target(m, 150, 150, hp=1000)
    assert m.peek(bomb + 0x1C8)
    rows = {}
    hist = collections.Counter()
    for idx in range(512):
        sh = m.call(0x80044550, idx) & M32
        if not sh:
            rows[idx] = None
            continue
        m.poke(tp + 0x1C4, sh)
        m.poke(tp + 0x14, 1000)
        m.poke(tp + 0x224, bomb)
        m.call(0x8003A374, tp)
        hp = m.peeks(tp + 0x14)
        raw = bytes(m.ram[(sh & 0x1FFFFFFF):(sh & 0x1FFFFFFF) + 0x10])
        r = raw[2 + 4]
        rows[idx] = (1000 - hp, r)
        hist[(r, 1000 - hp)] += 1
    d = json.load(open(r"D:/development/repo/alundra-casaengine-project-converter/data-extracted/data/BALANCE.BIN.json", encoding='utf-8-sig'))['EntriesBySpriteIndex']
    mon = [i for i in range(512) if d[i] and d[i].get('MaxHp', 0) > 0]
    print('sheets with a pointer:', sum(1 for v in rows.values() if v), 'monster sheets (MaxHp>0):', len(mon))
    print('bomb (attr 4, power 5) damage on HP-1000 targets, (response byte -> damage): count')
    for k, v in sorted(hist.items()):
        print('   response 0x%02X -> %4d : %d sheets' % (k[0], k[1], v))
    print('on the 147 monster sheets: ', dict(collections.Counter(rows[i][1] for i in mon if rows[i])))
    print('damage on the monster sheets:', dict(collections.Counter(rows[i][0] for i in mon if rows[i])))
    json.dump({str(k): v for k, v in rows.items()}, open('../results/bomb_damage_by_sheet.json', 'w'))


if __name__ == '__main__':
    main()
