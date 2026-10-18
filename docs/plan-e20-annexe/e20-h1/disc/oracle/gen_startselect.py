"""gen_startselect.py - E20.h1 discovery: the SECOND guard site 0x8002BB94, measured on the real code.  The per-frame function 0x8002BAEC (the one that ends with the generator step
0x8002BD0C-0x8002BD34) is run whole, from an empty memory, with its four callees (0x8002E38C, 0x8002E058, 0x8004B1D4, 0x80055570) stubbed; GetNumberOfItem, UseItem and SetPlayerHp run for real.
Vectors in the same format as gen_h1.py (./vec_ss/vec_startselect_8002BAEC.json); check_startselect.py compares them with a model of the block read in lst/pad_menu_8002B9F0.txt.
usage: python -B gen_startselect.py"""
import json
import os
import random
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import h1rig                  # noqa: E402
from h1rig import *           # noqa: E402,F403
import vtrace as V            # noqa: E402
from machine import M32       # noqa: E402

h1rig.STUBS.update({0x8002E38C: (1, 0), 0x8002E058: (0, 0), 0x8004B1D4: (0, 0), 0x80055570: (0, 0)})
import gen_h1 as G            # noqa: E402

CTRL = 0x800DC4B8           # g_playerControlFlags
GUARD2 = 0x800DC4B4         # a counter the function decrements (0x8002BC58)
BLOCK = 0x800C4980          # read by both sites
HOLD = 0x80126F28
PRESSED = 0x80126F2A
COUNTER_SS = 0x800986F0     # the Start+Select counter
MENU_REQ = 0x800DC4C4       # the menu request word written by 0x8002BCFC
UI = 0x8013FA38             # the four UI counters the head of the function moves
CURE_ALE = 0x27
OUT = os.path.join(HERE, 'vec_ss')
os.makedirs(OUT, exist_ok=True)


def make(rnd, label, counter, hold=0x900, pressed=0, ctrl=0, hero20=0, in_use=0, guard2=0, block=0, hp=5, ale=2, menu_check=0):
    stubs = {0x8002E38C: [0], 0x8002E058: [0], 0x8004B1D4: [0], 0x80055570: [menu_check]}
    m, calls, pending = G.new_rig(stubs)
    G.base_memory(m, stats=(2, 4), inv_counts={CURE_ALE: ale})
    m.poke(CTRL, ctrl)
    m.poke(HERO + 0x20, hero20)
    m.poke(HERO + 0x14, hp)
    m.poke(HERO + 0x18, 10)
    m.poke(HERO + 0x1CC, rnd.getrandbits(32))
    m.poke(IN_USE, in_use)
    m.poke16(HOLD, hold)
    m.poke16(PRESSED, pressed)
    m.poke(GUARD2, guard2)
    m.poke(BLOCK, block)
    m.poke(COUNTER_SS, counter)
    m.poke(MENU_REQ, rnd.getrandbits(32))
    m.poke(0x800A8284, rnd.getrandbits(32))
    m.poke(RNG, rnd.getrandbits(32))
    for k in (0x130, 0x134, 0x138, 0x13C):
        m.poke(UI + k, rnd.randint(0, 600))
    m.poke16(STATS + 0, hp)             # stats Hp
    m.poke16(STATS + 2, 10)             # stats HpMax
    vec = G.finish(m, calls, pending, 0x8002BAEC, [0], 'game', label, stubs)
    return vec


def main():
    rnd = random.Random(0x5E1EC7)
    vecs = []
    vecs.append(make(rnd, 'ss: counter 0 -> 1 (Start+Select held, nothing in use)', 0))
    vecs.append(make(rnd, 'ss: counter 0x50 -> 0x51', 0x50))
    vecs.append(make(rnd, 'ss: counter 0x77 -> 0x78', 0x77))
    vecs.append(make(rnd, 'ss: counter 0x78, life 5, two cure ales: one is used, life set to 0', 0x78, hp=5, ale=2))
    vecs.append(make(rnd, 'ss: counter 0x78, life 5, one cure ale: it is used, life set to 0', 0x78, hp=5, ale=1))
    vecs.append(make(rnd, 'ss: counter 0x78, life 5, no cure ale: nothing used, life set to 0', 0x78, hp=5, ale=0))
    vecs.append(make(rnd, 'ss: counter 0x78, life 10 (full), two cure ales', 0x78, hp=10, ale=2))
    vecs.append(make(rnd, 'ss: counter 0x78, life 0: nothing, the counter stays', 0x78, hp=0, ale=2))
    vecs.append(make(rnd, 'ss: counter 0x78, an item in use: the counter is reset, nothing else', 0x78, in_use=0x2B, hp=5, ale=2))
    vecs.append(make(rnd, 'ss: counter 0x50, the item 0x24 in use: reset', 0x50, in_use=0x24))
    vecs.append(make(rnd, 'ss: counter 0x50, hold = Start only (0x800): reset', 0x50, hold=0x800))
    vecs.append(make(rnd, 'ss: counter 0x50, hold = Select only (0x100): reset', 0x50, hold=0x100))
    vecs.append(make(rnd, 'ss: counter 0x50, hold = 0x940 (another button too): reset', 0x50, hold=0x940))
    vecs.append(make(rnd, 'ss: counter 0x50, hold = 0x1900 (a direction too): reset', 0x50, hold=0x1900))
    vecs.append(make(rnd, 'ss: counter 0x50, control flags 4: reset', 0x50, ctrl=4))
    vecs.append(make(rnd, 'ss: counter 0x50, control flags 0x40000000: reset', 0x50, ctrl=0x40000000))
    vecs.append(make(rnd, 'ss: counter 0x50, hero +0x20 != 0: reset', 0x50, hero20=1))
    vecs.append(make(rnd, 'ss: counter 0x50, [0x800DC4B4] != 0: reset', 0x50, guard2=3))
    vecs.append(make(rnd, 'ss: counter 0x50, [0x800C4980] != 0: reset', 0x50, block=1))
    # the Start menu site 0x8002BC90 on the same function
    vecs.append(make(rnd, 'menu: Start pressed, nothing in use: request written', 0, hold=0x800, pressed=0x800))
    vecs.append(make(rnd, 'menu: Start pressed, an item in use: refused', 0, hold=0x800, pressed=0x800, in_use=0x2B))
    vecs.append(make(rnd, 'menu: Start pressed, the same item 0x24 in use: refused', 0, hold=0x800, pressed=0x800, in_use=0x24))
    vecs.append(make(rnd, 'menu: pressed 0x2 (an other bit of 0x803)', 0, hold=0x2, pressed=0x2))
    vecs.append(make(rnd, 'menu: pressed 0x1', 0, hold=0x1, pressed=0x1))
    vecs.append(make(rnd, 'menu: pressed 0x4 (not in 0x803): nothing', 0, hold=0x4, pressed=0x4))
    vecs.append(make(rnd, 'menu: Start pressed with Select held (hold & 0x100): refused', 0, hold=0x900, pressed=0x800))
    vecs.append(make(rnd, 'menu: Start pressed, control flags 8: refused', 0, hold=0x800, pressed=0x800, ctrl=8))
    vecs.append(make(rnd, 'menu: Start pressed, hero +0x20 != 0: refused', 0, hold=0x800, pressed=0x800, hero20=2))
    vecs.append(make(rnd, 'menu: Start pressed, the extra test 0x80055570 answers 1: refused', 0, hold=0x800, pressed=0x800, menu_check=1))
    vecs.append(make(rnd, 'menu: Start pressed, [0x800DC4B4] = 2: refused', 0, hold=0x800, pressed=0x800, guard2=2))
    vecs.append(make(rnd, 'menu: Start pressed, [0x800C4980] != 0: refused', 0, hold=0x800, pressed=0x800, block=1))
    cov = set()
    for v in vecs:
        cov |= set(v.pop('_cov'))
    for i, v in enumerate(vecs):
        v['id'] = i
    path = os.path.join(OUT, 'vec_startselect_8002BAEC.json')
    with open(path, 'w') as f:
        json.dump({'function': '0x8002baec', 'name': 'startselect_and_menu', 'vectors': vecs}, f, separators=(',', ':'))
    print('%d vectors, %d pcs executed -> %s' % (len(vecs), len(cov), path))


if __name__ == '__main__':
    main()
