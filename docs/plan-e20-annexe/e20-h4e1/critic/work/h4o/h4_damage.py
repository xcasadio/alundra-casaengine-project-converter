"""h4_damage.py - the damage rule of the tomes: model vs the REAL ComputeNewHp (0x8003A374 -> ResolveBalanceTarget 0x8004464C), exhaustively over the 512 balance entries
of BALANCE.BIN (the sheet the binary selects for each sprite index at the level D = 0, REAL 0x80044550) and the eight spell attacks of the balance entries 16, 17, 19, 20, 21, 22, 23, 24
(the CurrentAttack the spell entity carries), for several HP values.

Model (docs/plan-e20-heros.md 1.6 K1 and AlundraDamage.cs): r = sheet[2 + (attr & 0xF)]; (r & 0xC0) == 0x40 -> HP 0 ; (r & 0xC0) != 0 -> unchanged ;
else damage = ((power + equipment) * r) >> 4, at least 1, HP = max(HP - damage, 0).  The spells have no equipment bit (attr bit 7 clear): equipment = 0.

usage: python -B h4_damage.py   exit 0 iff no mismatch.  Also prints the table of the responses to the four elements over the monster sheets (MaxHp > 0)."""
import sys
from collections import Counter
import spellworld as S
import world as W
from machine import s32, M32

ENT0 = 0x80127D30
STRIDE = 0x294
SPELL_ATTACKS = {16: (8, 32), 17: (8, 64), 19: (9, 64), 20: (10, 64), 21: (10, 128), 22: (11, 32), 23: (10, 32), 24: (11, 128)}
ATK = 0x801E6000


def model(attr, power, sheet_bytes, hp):
    r = sheet_bytes[2 + (attr & 0xF)]
    if attr == 0:
        return hp
    k = r & 0xC0
    if k == 0x40:
        return 0
    if k != 0:
        return hp
    dmg = (power * r) >> 4
    if dmg == 0:
        dmg = 1
    return hp - dmg if dmg < hp else 0


def main():
    m, STATS, placed = S.make(item=None, indices=(1,))
    A, T = ENT0 + 5 * STRIDE, ENT0 + 6 * STRIDE
    for e in (A, T):
        for off in range(0, STRIDE, 4):
            m.poke(e + off, 0)
    m.poke(A + 0x10, 2)
    m.poke(T + 0x10, 2)
    m.poke(0x80134360, 6)
    mism = 0
    n = 0
    cat = Counter()
    monster = {}
    for idx in range(512):
        rec = m.call(0x80044550, idx) & M32
        if rec == 0:
            continue
        sheet = bytes(m.rb(rec + k) for k in range(0x40))
        if sheet[2] > 0:                               # byte 2 = MaxHp: a monster sheet
            monster[idx] = sheet
        for spr, (attr, power) in SPELL_ATTACKS.items():
            m.poke8(ATK, attr)
            m.poke8(ATK + 1, power)
            m.poke(A + 0x1C8, ATK)
            m.poke(T + 0x1C4, rec)
            m.poke(T + 0x224, A)
            for hp in (1, 7, 40, 255, 1000):
                m.poke(T + 0x14, hp)
                dead = m.call(0x8003A374, T)
                got = s32(m.peek(T + 0x14))
                want = model(attr, power, sheet, hp)
                n += 1
                if got != want or (dead & 1) != (1 if want == 0 else 0):
                    mism += 1
                    if mism <= 5:
                        print('MISMATCH', idx, spr, hp, 'model', want, 'real', got, 'dead', dead)
                r = sheet[2 + (attr & 0xF)]
                cat[('kill' if (r & 0xC0) == 0x40 else 'immune' if (r & 0xC0) else 'normal')] += 1
    print('vectors', n, 'mismatches', mism, '(categories over all sheets x attacks x hp)', dict(cat))
    # table of responses of the monster sheets
    print('monster sheets (MaxHp > 0 at byte 2): %d of the sheets' % len(monster))
    for el, name in ((8, 'earth'), (9, 'water'), (10, 'fire'), (11, 'wind')):
        c = Counter()
        for idx, sh in monster.items():
            r = sh[2 + el]
            if (r & 0xC0) == 0x40:
                c['instant kill (0x4x)'] += 1
            elif (r & 0xC0) != 0:
                c['immune (0x80/0xC0)'] += 1
            else:
                c['r=%d' % r] += 1
        print('  %-5s (type %d, response byte %d):' % (name, el, 2 + el), dict(sorted(c.items(), key=lambda kv: -kv[1])))
    return mism


if __name__ == '__main__':
    sys.exit(1 if main() else 0)
