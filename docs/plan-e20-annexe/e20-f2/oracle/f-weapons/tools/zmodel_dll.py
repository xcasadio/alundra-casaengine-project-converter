"""zmodel_dll.py - the SAME rules written natively in the DLL convention (ADR-0026: absolute Z = binary - 1, terrain rest at T) and checked
equal to `zmodel.physics` minus 1 on a large random corpus (no emulator needed: zmodel.py itself is validated against the real code by validate.py).

DLL-native formulation (what a C# step for a controller-less proxy would compute):
    F  = FinalForceZ (after the decay of the tick)
    F > 0   : top = PosZ + ModZ + Depth + 1 + F ;  top > 0x7800000  ->  PosZ = 0x77FFFFE - ModZ - Depth, contact (the hero's H3-R2 cap)
              else PosZ += F
    F <= 0  : PosZ + ModZ + F < T   (strict)        ->  PosZ = T - ModZ, contact, ForceZ = 0 with Gravity
              else PosZ += F                         (reaching T exactly is NOT a landing: IsOnGround 1, contact 0)
    IsOnGround = PosZ <= Floor   (Floor = T on terrain; PosZ, NOT PosZ + ModZ: the binary compares +0x11C with FloorHeight, hero R6 uses PosZ + ModZ, identical while ModZ = 0)
"""
import random
import zmodel as Z
from zmodel import Ent, U, CAP_TOP


def physics_dll(e: Ent, T: int, G: int, V: int):
    e.coll = 0
    if e.izf != 0:
        e.forcez = 0 if ((e.izf & 0xFFFF) == 0x8000 and not e.gravity) else Z.s32(e.izf << 8)
        e.izf = 0
    elif e.gravity:
        v = e.forcez - (G << 8)
        lim = V << 8
        if abs(v) > lim:
            v = lim if v > 0 else -lim
        e.forcez = v
    e.finalz = e.forcez
    F = e.finalz
    e.terrain = T
    if F > 0:
        if e.posz + e.modz + e.depth + 1 + F > CAP_TOP:
            e.coll = 1
            e.posz = 0x77FFFFE - e.modz - e.depth
            if e.gravity:
                e.forcez = 0
        else:
            e.posz += F
    else:
        if e.posz + e.modz + F < T:
            e.coll = 1
            e.posz = T - e.modz
            if e.gravity:
                e.forcez = 0
        else:
            e.posz += F
    e.floor = T
    e.ground = 1 if e.posz <= e.floor else 0


def equivalence(n=200000, seed=3):
    rnd = random.Random(seed)
    bad = 0
    for k in range(n):
        T = rnd.choice([0, 16, 32, 48, 208, 400]) * U
        G, V = rnd.choice([(128, 4096), (3, 256), (128, 512), (50, 100), (1, 1)])
        modz = rnd.choice([0, 0, 8, -5, 3]) * U
        depth = rnd.choice([0, 16 * U - 1, 32 * U - 1])
        grav = rnd.random() < .5
        pz = rnd.choice([T + rnd.randrange(-3 * U, 120 * U), rnd.randrange(1800, 1919) * U + rnd.randrange(U)])
        fz = rnd.choice([0, rnd.randrange(-2 * U, 2 * U), -32768 * rnd.randrange(0, 40), 32768 * rnd.randrange(0, 40)])
        izf = rnd.choice([0, 0, 0, 0x8000, 256, -256, 1200])
        b = Ent(posz=pz, forcez=fz, gravity=grav, modz=modz, depth=depth, izf=izf)
        d = Ent(posz=pz - 1, forcez=fz, gravity=grav, modz=modz, depth=depth, izf=izf)
        for t in range(6):
            Z.physics(b, T, G, V)
            physics_dll(d, T, G, V)
            if (b.posz - 1, b.forcez, b.finalz, b.coll, b.ground, b.floor - 1, b.terrain) != (d.posz, d.forcez, d.finalz, d.coll, d.ground, d.floor, d.terrain):
                bad += 1
                if bad < 4:
                    print("DIFF", T, G, V, modz, depth, grav, pz, fz, izf, t, (b.posz - 1, b.forcez, b.coll, b.ground), (d.posz, d.forcez, d.coll, d.ground))
                break
    return bad


if __name__ == "__main__":
    print("binary-1 vs DLL-native: %d random 6-tick runs, differing: %d" % (200000, equivalence()))
