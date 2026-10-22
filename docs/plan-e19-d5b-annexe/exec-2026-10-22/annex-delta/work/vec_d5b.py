"""vec_d5b.py - binary vectors (the REAL pass 0x80038364 of ALUN_CD.EXE, via oracle_p) for the acceptance of D5b, written for the plan (2026-10-22, discovery d5b-disc).
Scenarios (all values relative, so the same in the binary's convention and in the DLL's, which is the binary's minus one raw unit):
  E22     the lift of map 22: platform 48x32x8 (flags 0x0560C0), hero on it, Fly +65536 from tick 3 for 64 ticks, stop, 3 ticks more.
  DESC    the same, descending -65536 for 24 ticks.
  CRATE_A / CRATE_B  a gravity crate (flags 0x3323E4, 21x15x16) alone on the rising platform, crate AFTER / BEFORE the platform in the slot order (the hero far away).
  CRATE_DESC         the crate on the descending platform (both orders).
  PILE_Z  hero on crate on platform, platform rising (Z): every rel is 0.
  PILE_XY hero on crate on platform, platform moving east +65536 per tick (and rising): the binary's DOUBLE force on the crate and the hero left behind.
  NPC     an NPC (flags 0x03A180 + Gravity, 20x14x32) on the platform: same rule as the crate.
  IMPULSE a passenger with an impulse (+0xF8 != 0) does NOT copy the Z force (copy_ignores_impulse).
Writes only to stdout.
"""
import sys
sys.path.insert(0, ".")
from oracle_p import *
from t3 import setup_xy   # noqa  (prints its own header; harmless)
sys.stdout.reconfigure(encoding="utf-8")

CRATE = dict(flags=0x3323E4, off=(-10, -7, 0), size=(21, 15, 16))
NPC = dict(flags=0x03A180, off=(-10, -7, 0), size=(20, 14, 32))
FAR = (300 << 16, 300 << 16)


def row(t, p, plat, pas, pas_h, hero=None, hero_h=None):
    c, q = p.snap(plat), p.snap(pas)
    s = "t%02d plat dz=%8d | pas rel=%7d ride=%s coll=%d gnd=%d ffz=%8d fz=%8d" % (t, c["z"] - row.z0, q["z"] - c["z"] - 8 * U, q["ride"], q["coll"], q["ground"], q["ffz"], q["fz"])
    s += " | crate dx(plat)=%7d ffx=%7d" % (q["x"] - c["x"], q["ffx"])
    if hero is not None:
        h = p.snap(hero)
        s += " | hero rel(pas)=%7d ride=%s coll=%d gnd=%d dx(crate)=%7d ffx=%7d" % (h["z"] - q["z"] - hero_h, h["ride"], h["coll"], h["ground"], h["x"] - q["x"], h["ffx"])
    print(s)


def e22(title, force, start_tick, stop_tick, n):
    p = P()
    X, Y = 100 << 16, 100 << 16
    zp = 1 + 40 * U
    p.add(1, PLAT_FLAGS_BIG, PLAT_OFF_BIG, PLAT_SIZE_BIG, (X, Y, zp))
    p.add(0, HERO_FLAGS, HERO_OFF, HERO_SIZE, (X, Y, zp + 8 * U))
    row.z0 = zp
    print("==", title)
    for t in range(n):
        if t == start_tick:
            p.put(1, "forcez", force)
        if t == stop_tick:
            p.put(1, "forcez", 0)
        p.tick()
        c, h = p.snap(1), p.snap(0)
        if t < 8 or t in (start_tick + 63, start_tick + 64, stop_tick - 1, stop_tick, stop_tick + 1, stop_tick + 2) or t == n - 1:
            print("t%02d plat dz=%8d | hero rel=%7d ride=%s coll=%d gnd=%d ffz=%8d" % (t, c["z"] - zp, h["z"] - c["z"] - 8 * U, h["ride"], h["coll"], h["ground"], h["ffz"]))
    return p


def crate(title, order_crate_first, force, start, stop, n, kind=CRATE, hero_on_crate=False, xy=False, impulse=False):
    p = P()
    X, Y = 100 << 16, 100 << 16
    zp = 1 + 40 * U
    if order_crate_first:
        pas, plat = 1, 2
    else:
        plat, pas = 1, 2
    hero = 0
    p.add(plat, PLAT_FLAGS_BIG, PLAT_OFF_BIG, PLAT_SIZE_BIG, (X, Y, zp))
    p.add(pas, kind["flags"], kind["off"], kind["size"], (X, Y, zp + 8 * U))
    ph = kind["size"][2] * U
    if hero_on_crate:
        p.add(hero, HERO_FLAGS, HERO_OFF, HERO_SIZE, (X, Y, zp + 8 * U + ph))
    else:
        p.add(hero, HERO_FLAGS, HERO_OFF, HERO_SIZE, (FAR[0], FAR[1], 1))
    if impulse:
        p.put(pas, "izf", 1280) if hasattr(p, "put") else None
    row.z0 = zp
    print("==", title)
    for t in range(n):
        if t == start:
            p.put(plat, "forcez", force)
            if xy:
                setup_xy(p, plat, 85, 24, 65536, 0, 65536)
        if t == stop:
            p.put(plat, "forcez", 0)
        p.tick()
        row(t, p, plat, pas, ph, hero if hero_on_crate else None, 16 * U)
    return p


if __name__ == "__main__":
    e22("E22 lift of map 22: platform rises +1 px/tick from tick 3 for 64 ticks, hero on it", 65536, 3, 67, 72)
    e22("DESC platform descends -1 px/tick from tick 3 for 24 ticks, hero on it", -65536, 3, 27, 32)
    for first in (False, True):
        crate("CRATE rising +1 px/tick t3..t8, crate %s the platform in the slot order, hero far" % ("BEFORE" if first else "AFTER"), first, 65536, 3, 9, 14)
    for first in (False, True):
        crate("CRATE descending -1 px/tick t3..t8, crate %s the platform in the slot order, hero far" % ("BEFORE" if first else "AFTER"), first, -65536, 3, 9, 14)
    crate("NPC (gravity, 20x14x32) rising +1 px/tick t3..t8, NPC AFTER the platform", False, 65536, 3, 9, 14, kind=NPC)
    crate("PILE_Z hero on crate on platform, rising +1 px/tick t3..t8", False, 65536, 3, 9, 14, hero_on_crate=True)
    crate("PILE_XY hero on crate on platform, platform east +1 px/tick and rising +1 px/tick t3..t8", False, 65536, 3, 9, 14, hero_on_crate=True, xy=True)
