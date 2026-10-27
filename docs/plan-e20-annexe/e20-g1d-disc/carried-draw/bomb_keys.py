"""READ-ONLY oracle run (a COPY of docs/plan-e20-annexe/e20-h3/critic/run is in ./oracle_h3, never edited): the REAL code of ALUN_CD.EXE (France) in the MIPS interpreter.
Scenario = scen_bomb1.py (Circle at tick 1 with the item 0x23: the bomb is born in the hands, lifted to Z 32, carried).
After EACH tick the binary's own sort driver 0x800399B8 is run (as docs/plan-e20-annexe/e20-f1/oracle/keyprobe.py does) and the depth key +0x1BC of the hero (slot 0) and of
the bomb (sprite type 2) is printed with the fields it is made of: status +0x10, flags +0x6C, anim flags +0xB4, carrier +0x28, IDSV +0x1A8, Y +0x118, Z +0x11C, key +0x1BC.
The OT slot is 16*min(key>>20, 59)+6 (emitter 0x8002DBD8, sra 0x14, clamp 0x3B); a larger key is drawn later (in front) (docs/plan-e19-g3c-annexe/discovery/notes.md 3.1)."""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
O = os.path.join(HERE, 'oracle_h3')
sys.path.insert(0, O)
sys.path.insert(0, r'C:\Users\casad\AppData\Roaming\Python\Python314\site-packages')
from scen_lib import *  # noqa: E402,F401  (mk, run, W, ents, snap, ...)

BOMB_SPRITE_TYPE = 2


def s16(v):
    v &= 0xFFFF
    return v - 0x10000 if v & 0x8000 else v


def slot(key):
    return 16 * min((key & 0xFFFFFFFF) >> 20, 59) + 6


rows = []


def on_tick(m, t, rec):
    W.run_pass(m, 0x800399B8)  # the sort driver, run after the tick like the tail 0x8003B414 does
    hero = HERO
    bombs = [i for i, e in rec['ents'].items() if e['sp'] == BOMB_SPRITE_TYPE and e['st'] in (1, 2, 3)]
    if not bombs:
        return
    b = W.ent(bombs[0])

    def f(e):
        return dict(st=m.peeks(e + 0x10), fl=m.peek(e + 0x6C), afl=m.peek(e + 0xB4), car=m.peek(e + 0x28), idsv=m.peeks(e + 0x1A8),
                    y=m.peeks(e + 0x118), z=m.peeks(e + 0x11C), key=m.peek(e + 0x1BC) & 0xFFFFFFFF, anim=m.peeks(e + 0x90))
    rows.append((t, f(hero), f(b)))


m, STATS = mk(0x23)
log = run(m, 120, lambda t: W.CIRCLE if t == 1 else 0, on_tick=on_tick)

print("tick | HERO st fl      afl  car       idsv  Y(px)    Z(px)  anim key        slot | BOMB st fl       afl  car       idsv  Y(px)    Z(px)  anim key        slot | bomb key - hero key | bomb drawn")
prev = None
for t, h, b in rows:
    d = (b['key'] - h['key'])
    verdict = 'AFTER (in front)' if d > 0 else ('BEFORE (behind)' if d < 0 else 'equal key')
    line = (t, h['key'], b['key'], h['idsv'], b['idsv'], b['car'] != 0, b['st'])
    if t <= 6 or t % 10 == 0 or prev != (verdict, b['idsv'], h['idsv']):
        print("%4d | %2d %08X %02X %08X %2d  %8.3f %7.3f  %3d %08X %3d | %2d %08X %02X %08X %2d  %8.3f %7.3f  %3d %08X %3d | %+d | %s" % (
            t, h['st'], h['fl'], h['afl'] & 0xFF, h['car'], h['idsv'], h['y'] / 65536, h['z'] / 65536, h['anim'], h['key'], slot(h['key']),
            b['st'], b['fl'], b['afl'] & 0xFF, b['car'], b['idsv'], b['y'] / 65536, b['z'] / 65536, b['anim'], b['key'], slot(b['key']), d, verdict))
    prev = (verdict, b['idsv'], h['idsv'])
print()
ahead = sum(1 for _, h, b in rows if b['key'] > h['key'])
behind = sum(1 for _, h, b in rows if b['key'] < h['key'])
eq = sum(1 for _, h, b in rows if b['key'] == h['key'])
print("ticks with a bomb in the world: %d ; bomb key > hero key: %d ; < : %d ; = : %d" % (len(rows), ahead, behind, eq))
