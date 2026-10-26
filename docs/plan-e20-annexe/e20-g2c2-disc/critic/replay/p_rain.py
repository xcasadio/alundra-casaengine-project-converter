"""p_rain.py (critic) - read-only: the torches C[15] of map 92 spawns (record 68, 0x8A) get `0x62 [98, 68, 16, 0]` = flags |= 0x10 (DeactivateOnImpact). What does the REAL C70 / E20 / E0 do with such a torch
that is FALLING from the air and lands (no hero action)?  A control run without the 0x10 bit shows the plain falling torch (the 87/90/94 scripted ones, after their hand-over).
Run: python -B -I -X utf8 p_rain.py"""
import os, sys
HERE = os.path.dirname(os.path.abspath(__file__))
ORC = os.path.join(HERE, 'work', 'orc')
sys.path.insert(0, ORC)
sys.path.insert(0, HERE)
import gl
import k0lib as K
import c4_scen as S
from machine import M32

C70, E20, E0 = 0x8007B7B0, 0x8007F6C8, 0x8007ED10


def flame_slot(m):
    for s_ in K.active(m):
        if m.peek(K.ent(s_) + 0x68) == 0xC:
            return s_
    return None


def run(or_bits, n=140, lift_px=60):
    S.widen('map_86', 137)
    m = gl.world(keep={C70, E20, E0}, hero_px=(60, 60))
    c = gl.add_crate(m, 150, 150, mapname='map_86', sprite=137)
    e = K.ent(c)
    m.poke(e + 0x11C, (m.peeks(e + 0x11C) + lift_px * 65536) & M32)
    m.poke(e + 0x1E0, (m.peeks(e + 0x1E0) + lift_px * 65536) & M32)
    pads, nn = gl.pad_script([(n, 0)])
    log = []

    def post(mm, t, r):
        fs = flame_slot(mm)
        fl = None
        if fs is not None:
            fe = K.ent(fs)
            fl = (mm.peek(fe + 0x10), mm.peek(fe + 0x88), round(mm.peeks(fe + 0x114) / 65536, 1), round(mm.peeks(fe + 0x118) / 65536, 1), round(mm.peeks(fe + 0x11C) / 65536, 1))
        log.append((t, mm.peek(e + 0x10), mm.peek(e + 0x88), hex(mm.peek(e + 0x6C)), round(mm.peeks(e + 0x11C) / 65536, 1), mm.peek(e + 0x144), fl))
    def pre(mm, t):
        if t == 4 and or_bits:
            mm.poke(e + 0x6C, mm.peek(e + 0x6C) | or_bits)   # the 0x62 of C[15], once the torch is in the air
    gl.run(m, nn, pads, crates=[c], post=post, pre=pre)
    out = ['flags |= %#x: (tick, torch status, anim, flags, z, onground, flame(status, anim, x, y, z))' % or_bits]
    prev = None
    for x in log:
        key = x[1:4] + (x[5],) + (x[6] is not None,)
        if key != prev:
            out.append(str(x))
        prev = key
    return out


if __name__ == '__main__':
    for bits in (0x10, 0):
        print('\n'.join(run(bits)))
        print()
