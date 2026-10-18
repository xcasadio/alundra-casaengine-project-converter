"""my_damage.py - critic: bomb damage (attack attribute byte 4, power 5) on every balance sheet, from MY reading of 0x8004464C, checked against the REAL code."""
import json, collections
import spellworld as S
import world as W
from machine import M32, s32
BAL = json.load(open(r"D:/development/repo/alundra-casaengine-project-converter/data-extracted/data/BALANCE.BIN.json", encoding='utf-8-sig'))['EntriesBySpriteIndex']

def my_rule(r, hp, power=5):
    rm = r & 0xC0
    if rm == 0x40: return 0
    if rm != 0: return hp
    d = (power * r) >> 4
    if d == 0: d = 1
    return hp - d if d < hp else 0

def main():
    m, STATS, placed = S.make(item=None)
    # real code: ResolveBalanceTarget(attack ptr, sheet ptr, hp); the attack record = 2 bytes {attr, power} at ATK
    ATK = 0x801E6000
    m.poke8(ATK, 4); m.poke8(ATK + 1, 5)
    # the blob is loaded by O.setup: sheet record for index i at level d via REAL 0x80044550
    bad = 0
    cats = collections.Counter()
    per = {}
    for i in range(512):
        sh = m.call(0x80044550, i) & M32
        if sh == 0: continue
        for hp in (1, 2, 3, 5, 6, 10, 50, 255):
            real = s32(m.call(0x8004464C, ATK, sh, hp))
            r = m.rb(sh + 2 + 4)
            exp = my_rule(r, hp)
            if real != exp:
                bad += 1
                print('MISMATCH sheet', i, 'hp', hp, 'real', real, 'mine', exp, 'r', r)
        r = m.rb(sh + 6)
        per[i] = r
    print('mismatches', bad)
    # JSON cross-check of the sheet byte (DamageResponses[3] is sheet[6])
    jb = 0
    for i, r in per.items():
        e = BAL[i]
        if e['DamageResponses'][3] != r: jb += 1
    print('sheet byte vs JSON DamageResponses[3] mismatches:', jb, 'of', len(per))
    # classification over the map-sprite sheets 256..511 that have MaxHp > 0 (monster-like), and the global ones
    def outcome(r, mx):
        rm = r & 0xC0
        if rm == 0x40: return 'kill'
        if rm != 0: return 'immune'
        d = max(1, (5 * r) >> 4)
        return 'dmg%d' % d
    for rng, name in ((range(0, 256), 'global 0..255'), (range(256, 512), 'map sprites 256..511')):
        c = collections.Counter(outcome(per[i], BAL[i]['MaxHp']) for i in rng if i in per)
        print(name, dict(sorted(c.items())))
    mons = [i for i in range(256, 512) if i in per and BAL[i]['MaxHp'] > 0]
    c = collections.Counter(outcome(per[i], BAL[i]['MaxHp']) for i in mons)
    print('map sheets with MaxHp>0:', len(mons), dict(sorted(c.items())))
    mons2 = [i for i in range(256, 512) if i in per and BAL[i]['MaxHp'] > 0 and BAL[i]['AttackCount'] > 0]
    c = collections.Counter(outcome(per[i], BAL[i]['MaxHp']) for i in mons2)
    print('map sheets with MaxHp>0 and attacks:', len(mons2), dict(sorted(c.items())))
    kills = [i for i in range(512) if i in per and (per[i] & 0xC0) == 0x40]
    print('kill sheets (all):', len(kills), kills)
    json.dump({str(i): per[i] for i in per}, open('../results/my_bomb_r_by_sheet.json', 'w'))
main()
