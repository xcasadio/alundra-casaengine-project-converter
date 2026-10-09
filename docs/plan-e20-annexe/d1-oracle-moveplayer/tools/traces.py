"""traces.py - the pinned-value tables a plan can write in advance (DLL convention = binary - 1), each produced by the REAL code in the interpreter
(zsim.py), asserted equal to zmodel.py (binary) and to zmodel_dll.py (DLL-native, binary - 1).  Output: values.md-style text on stdout.

Scenarios use the montage of a synthetic test (flat terrain, one controller-less proxy, no other entity, no XY force): terrain T, map gravity G / viscosity V.
"""
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from zsim import *
import zmodel as Z
import zmodel_dll as D
sys.stdout.reconfigure(encoding="utf-8")


def flat(hb):
    return lambda c, r: (hb, 0, 0, 0)


def scenario(title, hb, G, V, flags, posz_dll, forcez0=0, ticks=8, acts=None, off=(0, 0, 0), size=(0, 0, 0), show=None, note=""):
    """posz_dll: the DLL-convention start; the binary start is +1.  acts: {tick: ('1B', v) | ('grav',) | ('posz', dll)} applied before the physics of that tick."""
    acts = acts or {}
    T = hb * 16 * U
    s = Sim(gravity=G, visc=V, cells=flat(hb))
    s.add(1, flags, off=off, size=size, pos=(100 << 16, 100 << 16, posz_dll + 1))
    s.put(1, "forcez", forcez0)
    s.end_pass(ent(1))
    depth = (size[2] << 16) - 1 if size[2] else 0
    b = Z.Ent(posz=posz_dll + 1, forcez=forcez0, gravity=bool(flags & 0x100), modz=off[2] << 16, depth=depth)
    d = Z.Ent(posz=posz_dll, forcez=forcez0, gravity=bool(flags & 0x100), modz=off[2] << 16, depth=depth)
    print("\n### %s\nterrain %d px, G %d V %d, flags %#x, start PosZ %d (binary %d), ForceZ %d %s" % (title, hb * 16, G, V, flags, posz_dll, posz_dll + 1, forcez0, note))
    print("| tick | PosZ | ForceZ | FinalForceZ | Coll | IsOnGround | binary PosZ |")
    print("|---|---|---|---|---|---|---|")
    rows = []
    for t in range(1, ticks + 1):
        a = acts.get(t)
        if a:
            if a[0] == "1B":
                s.put(1, "forcez", s32(((a[1] ^ 0x8000) - 0x8000) << 8)); Z.op1B(b, a[1] & 0xFF, a[1] >> 8); Z.op1B(d, a[1] & 0xFF, a[1] >> 8)
            elif a[0] == "grav":
                s.put(1, "flags", s.get(1, "flags") ^ 0x100); b.gravity = not b.gravity; d.gravity = not d.gravity
            elif a[0] == "posz":
                s.put(1, "posz", a[1] + 1); b.posz = a[1] + 1; d.posz = a[1]
        s.tick(rebuild=False)
        Z.physics(b, T, G, V); D.physics_dll(d, T, G, V)
        q = s.snap(1)
        emu = (q["posz"], q["forcez"], q["finalz"], q["coll"], q["ground"])
        assert emu == (b.posz, b.forcez, b.finalz, b.coll, b.ground), ("emu != model", t, emu, b)
        assert (b.posz - 1, b.forcez, b.finalz, b.coll, b.ground) == (d.posz, d.forcez, d.finalz, d.coll, d.ground), ("dll != binary-1", t)
        rows.append((t, d.posz, d.forcez, d.finalz, d.coll, d.ground, b.posz))
    for r in rows:
        if show is None or r[0] in show or r[0] <= 3 or r[0] >= ticks - 2:
            print("| %d | %d | %d | %d | %d | %d | %d |" % r)
    return rows


# Z-1 rise without gravity (what 63 [128,128,1] + 1B [128,0] does to I33 in 260)
scenario("Z-1  rise, gravity off, ForceZ +32768 from rest on flat 0", 0, 128, 4096, 0x80, 0, 32768, 5)
# Z-2 fall without gravity (47 C[4]: 17, 64, 1B [128,255]): from 160 px, terrain 16 px, 192 ticks, wait ends at the 193rd call
r = scenario("Z-2  fall, gravity off, ForceZ -32768 from 160 px over a 16 px terrain (Sara of 47)", 1, 128, 4096, 0x80, 160 * U, -32768, 192, show={191, 192})
# Z-3 drop of a gravity entity from 5 px over flat 0 (first landing)
scenario("Z-3  gravity entity dropped from 5 px on flat 0, G 128 V 4096", 0, 128, 4096, 0x180, 5 * U, 0, 8)
# Z-4 rest with gravity
scenario("Z-4  at rest with gravity on flat 0 (T = 0): contact every tick", 0, 128, 4096, 0x180, 0, 0, 4)
# Z-5 rest without gravity
scenario("Z-5  at rest WITHOUT gravity on flat 0", 0, 128, 4096, 0x80, 0, 0, 3)
# Z-6 below the terrain (without gravity, F = 0): raised at the next tick (landing test with F = 0)
scenario("Z-6  no gravity, F = 0, 1 unit BELOW the terrain (T = 16 px): lifted to T with contact", 1, 128, 4096, 0x80, 16 * U - 1, 0, 3)
scenario("Z-6b no gravity, F = 0, exactly AT the terrain: nothing happens (strict landing)", 1, 128, 4096, 0x80, 16 * U, 0, 3)
# Z-7 terminal velocity, maps 128/4096 and 3/256
scenario("Z-7  terminal velocity: gravity entity from 600 px over flat 0, G 128 V 4096", 0, 128, 4096, 0x180, 600 * U, 0, 80, show={30, 31, 32, 33, 34, 35})
scenario("Z-7b maps 159/160: G 3 V 256 (terminal 256 << 8 = 65536 after 86 ticks), from 300 px", 0, 3, 256, 0x180, 300 * U, 0, 100, show={84, 85, 86, 87})
# Z-8 script force overwritten by the decay in the same tick for a gravity entity
scenario("Z-8  1B [128,0] on a gravity entity (no 17 first): the decay of the same tick eats it", 0, 128, 4096, 0x180, 10 * U, 0, 4, acts={1: ("1B", 128)})
# Z-9 ceiling cap (no gravity, rising 64 px/tick from 1792 px, size z 32)
scenario("Z-9  cap 0x7800000: no gravity, ForceZ +64 px/tick from 1792 px, SizeZ 32", 0, 128, 4096, 0x80, 1792 * U, 64 * U, 4, size=(0, 0, 32))
