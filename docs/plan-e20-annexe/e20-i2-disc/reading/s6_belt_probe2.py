"""s6_belt_probe2.py - second READ-ONLY probe: belts on deep water, lock on water, airborne (unmatched level) drift, a cell in the map border, the hero stepping onto and off a belt.
Same harness (copy in ./oc/base), same rules as s5: every number is read from the emulated binary."""
import os, sys
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, r'C:\Users\casad\AppData\Roaming\Python\Python314\site-packages')
sys.path.insert(0, os.path.join(HERE, 'oc', 'base'))
sys.path.insert(0, HERE)
import rworld as R
from s5_belt_probe import probe, show

RIGHT, LEFT, DOWN, UP, CRO, TRI = R.RIGHT, R.LEFT, R.DOWN, R.UP, R.CRO, R.TRI


def lake(sl, c0=10, c1=40, r0=5, r1=40):
    return R.Layout().rect(c0, r0, c1, r1, word=0x800, sl=sl)


if __name__ == '__main__':
    # E1: deep water belt east, boots 0x1B, hero floats (start state 0x1D = 29), no pad
    res, rec = probe('E1', lake(0x74), (500, 200), 30, [(1, 0)], inv='b1b', hero_anim=29)
    show('E1 deep belt east (0x74), boots 0x1B, floating, no pad', rec, [1, 2, 3, 5, 10, 20, 30])
    # E1b: swimming against / with the belt
    res, rec = probe('E1b', lake(0x74), (500, 200), 40, [(1, RIGHT)], inv='b1b', hero_anim=29)
    show('E1b deep belt east, swimming Right', rec, [2, 5, 10, 20, 30, 40])
    res, rec = probe('E1c', lake(0x74), (500, 200), 40, [(1, LEFT)], inv='b1b', hero_anim=29)
    show('E1c deep belt east, swimming Left (against)', rec, [2, 5, 10, 20, 30, 40])
    # E2: lock on deep water, pad held: the state is forced to 0x1D every tick
    res, rec = probe('E2', lake(0xF4), (500, 200), 30, [(1, RIGHT)], inv='b1b', hero_anim=29)
    show('E2 deep LOCK belt east (0xF4), Right held', rec, [1, 2, 5, 10, 20, 30])
    # E2b: lock on deep water with a dive press at tick 10 (no dive expected)
    res, rec = probe('E2b', lake(0xF4), (500, 200), 30, [(1, RIGHT), (10, RIGHT | TRI), (11, RIGHT)], inv='b1b', hero_anim=29)
    show('E2b deep LOCK belt east, Right held + Triangle at 10', rec, [9, 10, 11, 12, 20])
    # E3: airborne above a gate-0x08-only belt: the hero jumps (Cross) from rest; drift only while the level does not match
    lay = R.Layout().rect(10, 5, 40, 40, hb=0, sl=0x78)
    res, rec = probe('E3', lay, (500, 200), 60, [(1, 0), (5, CRO), (6, 0)])
    show('E3 gate 0x08-only belt east (0x78), jump (Cross) at tick 5', rec, [3, 4, 5, 6, 8, 12, 16, 20, 24, 28, 32, 36, 40, 50])
    # E4: a lock belt on dry ground, then the pad Right and Cross (jump) pressed
    lay = R.Layout().rect(10, 5, 40, 40, hb=0, sl=0xF4)
    res, rec = probe('E4', lay, (500, 200), 40, [(1, RIGHT), (10, RIGHT | CRO), (11, RIGHT)])
    show('E4 dry LOCK belt east (0xF4), Right held, Cross at 10', rec, [5, 9, 10, 11, 12, 20, 30, 40])
    # E5: step off a belt: east belt for columns 10..20 only (x < 504), hero walks Right from 480
    lay = R.Layout().rect(10, 5, 20, 40, hb=0, sl=0x74)
    res, rec = probe('E5', lay, (470, 200), 40, [(1, 0)])
    show('E5 idle at x=470 on an east belt that ends at column 20 (x 504): drift stops after the last cell', rec, [1, 10, 20, 30, 36, 37, 38, 40])
