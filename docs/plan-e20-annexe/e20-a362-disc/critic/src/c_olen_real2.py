"""c_olen_real.py - critic: Olen's bomb against the lid in the oracle, WITH THE REAL CELLS OF THE MAP 362 (Flags word of each of the 52x60 cells poked into the oracle's cell table), the real lid (global 243) at its post-C[3] position (212,100,z32)
and the bomb born where Olen's 0x8B puts it (204, OLENY, z 32+32), facing north (0x10).  The native C62 is neutralized (+0x78 = 0, the map program C[5] replaces it); `Set anim 2` once at T4, `Set anim 1` at T5 (the explosion).
The hero is parked far away.  Prints the bomb's x,y,z when it changes, the lid's TouchingEntity (+0x224) and the first tick of each box.   usage: c_olen_real.py OLENY T4 T5 [N]"""
import sys, json
import spellworld as S
import world as W
from machine import s32, M32
from scen_lib import *
from scen_zone import spawn
OLENY = float(sys.argv[1]); T4 = int(sys.argv[2]); T5 = int(sys.argv[3])
N = int(sys.argv[4]) if len(sys.argv) > 4 else T5 + 80
d = json.load(open("D:/development/repo/alundra-casaengine-project-converter/data-extracted/data/map_362.json", encoding='utf-8-sig'))
tiles = {(t['TileX'], t['TileY']): t for t in d['Map']['MapTiles']}
m, STATS = mk(None, indices=(1, 2, 243, 253))
for row in range(60):
    for col in range(52):
        a = W.TILES + 0x604 + row * W.ROW + col * 8
        m.poke(a, tiles[(col, row)]['Flags'] & 0xFFFFFFFF)
H = W.ent(0)
# park the hero far from the scene: (204, 240) = col 8, row 15
m.poke(H + 0x114, int(204 * 65536) & M32); m.poke(H + 0x118, int(240 * 65536) & M32); m.poke(H + 0x11C, int(32 * 65536) & M32)
pad = W.Pad()
lid = spawn(m, H, 0, 243, 212, 100, 32)
b = spawn(m, H, 0, 2, 204, OLENY, 64, 0x10)
assert lid and b
m.poke(b + 0x78, 0)
m.poke(b + 0x6C, m.peek(b + 0x6C) & ~0x100)   # the script's Low gravity (0x17): the Gravity flag cleared; Fly 0 = ForceZ 0 (the bomb hovers)
print('cell (8,5) %#x (8,6) %#x (8,7) %#x ; lid pos (%.2f,%.2f,%.2f) ; bomb born (%.2f,%.2f,%.2f)' % (tiles[(8,5)]['Flags'], tiles[(8,6)]['Flags'], tiles[(8,7)]['Flags'], m.peeks(lid+0x114)/65536, m.peeks(lid+0x118)/65536, m.peeks(lid+0x11C)/65536, m.peeks(b+0x114)/65536, m.peeks(b+0x118)/65536, m.peeks(b+0x11C)/65536))
prev = None
firstbox = None; hit = None
for t in range(1, N + 1):
    m.tick_no = t
    j0 = len(m.journal)
    W.tick(m, pad, 0, check_pad=False)
    if t == T4:
        m.poke(b + 0x88, 2)
        m.poke(b + 0x6C, m.peek(b + 0x6C) | 0x100)   # the script's High gravity (0x16)
    if t == T5: m.poke(b + 0x88, 1)
    jr = [(x[1], x[2] if x[1] == 'sound' else tuple(x[2][:2])) for x in m.journal[j0:] if x[1] in ('sound', 'effect_3BDD8') and not (x[1] == 'sound' and x[2] == 0)]
    box = m.peeks(b + 0x1D4)
    touch = m.peek(lid + 0x224)
    if box and firstbox is None: firstbox = t
    if touch and hit is None: hit = t
    st = (round(m.peeks(b + 0x114) / 65536, 2), round(m.peeks(b + 0x118) / 65536, 2), round(m.peeks(b + 0x11C) / 65536, 2), m.peeks(b + 0x10), m.peeks(b + 0x90), bool(box), bool(touch), m.peeks(lid + 0x10))
    key = (st, tuple(map(str, jr)))
    if key != prev and (t < T4 + 40 or t >= T5 - 1 or True):
        # print only landing/rest changes in x,y,z compressed: skip per-tick y changes while sliding
        pass
    prev = key
    if t in (T4, T4 + 10, T4 + 25, T4 + 30, T4 + 40, T4 + 60, T5 - 1, T5, T5 + 1, T5 + 15, T5 + 16, T5 + 17, T5 + 18, T5 + 19, T5 + 20, T5 + 25):
        print('t%3d bomb x %.2f y %.2f z %.2f status %d canim %d box %s | lid touching %s status %d %s' % ((t,) + st + (jr if jr else '',)))
print('first box tick %s ; lid touched (+0x224) at tick %s ; T5 = %d  => box starts at T5+%s, contact at T5+%s' % (firstbox, hit, T5, (firstbox - T5) if firstbox else None, (hit - T5) if hit else None))
