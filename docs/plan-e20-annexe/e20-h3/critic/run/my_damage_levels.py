"""my_damage_levels.py - critic: the balance level (map header +0xB, 0..11) selects the sheet variant (0x80044550); bomb damage per level on REAL code."""
import collections, json
import spellworld as S
from machine import M32, s32
m, STATS, placed = S.make(item=None)
ATK = 0x801E6000
m.poke8(ATK, 4); m.poke8(ATK + 1, 5)
BAL = json.load(open(r"D:/development/repo/alundra-casaengine-project-converter/data-extracted/data/BALANCE.BIN.json", encoding='utf-8-sig'))['EntriesBySpriteIndex']
def outcome(r):
    rm = r & 0xC0
    if rm == 0x40: return 'kill'
    if rm: return 'immune'
    return 'dmg%d' % max(1, (5 * r) >> 4)
rows = {}
for L in range(0, 12):
    m.call(0x80044520, L)
    per = {}
    for i in range(256, 512):
        sh = m.call(0x80044550, i) & M32
        per[i] = (m.rb(sh + 6), m.rb(sh + 2), m.rb(sh + 0))   # response type4, MaxHp byte, level byte
    rows[L] = per
    mons = [i for i in per if per[i][1] > 0]
    c = collections.Counter(outcome(per[i][0]) for i in mons)
    print('level %2d  map sheets with MaxHp>0: %d  %s' % (L, len(mons), dict(sorted(c.items()))))
# how many sheets change outcome between level 0 and any other level
chg = collections.defaultdict(list)
for L in range(1, 12):
    for i in rows[0]:
        if outcome(rows[0][i][0]) != outcome(rows[L][i][0]):
            chg[L].append(i)
for L in range(1, 12):
    print('level', L, 'sheets whose bomb outcome differs from level 0:', len(chg[L]), chg[L][:20])
json.dump({str(L): {str(i): rows[L][i] for i in rows[L]} for L in rows}, open('../results/my_bomb_by_level.json', 'w'))
