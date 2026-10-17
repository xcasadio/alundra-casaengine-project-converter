"""zmodel.py - pure-python rules of the binary's vertical step for ONE record entity with no XY force (binary convention: terrain rest = T + 1).

Sources (all read in ALUN_CD.EXE (France) and run by the interpreter in zsim.py, see validate.py):
  force pass            0x80036828, non-hero branch 0x80036A84-0x80036BB8  (IZF impulse / gravity decay with two-sided terminal / FinalForceZ = ForceZ)
  ComputeZPosition      0x800375E0  (F > 0: cap 0x80036D94 ; F <= 0: landing test 0x80036BFC)
  end pass              0x80038064 -> FloorHeight 0x80037F28 (= T + 1 on terrain), IsOnGround = !(Floor < PosZ)  [PosZ, not PosZ + ModZ]
  ComputeXYPosition     0x80037730 returns at once when FinalForceX and FinalForceY are both 0: no 3 px magnet for an entity with no XY force
Handlers: 0x1B 0x8003D7D0, 0x20 0x8003D9BC, 0x22 0x8003DA70 (see t_handlers.out).

All values are 16.16 raw integers (1 px = 65536).  `dll(x)` = binary - 1 for an absolute Z (ADR-0026).
"""
from dataclasses import dataclass, field

U = 65536
CAP_TOP = 0x7800000            # 1920 px


def s32(v):
    v &= 0xFFFFFFFF
    return v - (1 << 32) if v & 0x80000000 else v


@dataclass
class Ent:
    posz: int
    forcez: int = 0
    gravity: bool = False
    modz: int = 0              # OffsetZ << 16
    depth: int = 0             # SizeZ << 16 - 1 (0 when SizeZ == 0)
    izf: int = 0               # pending impulse word (UpdateAnimation +0xF8), one tick
    finalz: int = 0
    coll: int = 0
    ground: int = 0
    floor: int = 0
    terrain: int = 0
    # memos of the waits (Parameters[1] = pc, Parameters[2])
    memo_pc: int = 0
    memo: int = 0


# ---------------------------------------------------------------------------------------------------------------------
# physics pass
# ---------------------------------------------------------------------------------------------------------------------
def physics(e: Ent, T: int, G: int, V: int, mut=None):
    """One pass of P for the entity.  T = terrain height under the box at the position of this tick (flat: constant)."""
    mut = mut or set()
    e.coll = 0                                          # 0x800383B4
    modded = e.posz + e.modz                            # 0x800383D0
    # --- force pass (non-hero branch)
    if e.izf != 0:
        if (e.izf & 0xFFFF) == 0x8000 and not e.gravity:
            e.forcez = 0
        else:
            e.forcez = s32(e.izf << 8)
        e.izf = 0
    elif e.gravity:
        v = e.forcez - (G << 8)
        lim = V << 8
        if "one_sided" in mut:
            if v < -lim: v = -lim
        elif abs(v) > lim:
            v = lim if v > 0 else -lim
        e.forcez = v
    e.finalz = e.forcez                                 # 0x80036BB8
    F = e.finalz
    # --- ComputeZPosition
    e.terrain = T
    if F > 0:
        target = modded + e.depth + F
        if target > CAP_TOP:
            e.coll = 1
            e.posz = 0x77FFFFF - e.modz - e.depth
            if e.gravity:
                e.forcez = 0
        else:
            e.posz += F
    else:
        t1 = modded + F
        land = (T > t1) if "strict_landing" in mut else (T >= t1)
        if land:
            e.coll = 1
            e.posz = T + 1 - e.modz
            if e.gravity:
                e.forcez = 0
        else:
            e.posz += F
    # --- end pass
    e.floor = T + 1
    pz = (e.posz + e.modz) if "ground_modded" in mut else e.posz
    e.ground = 1 if not (e.floor < pz) else 0


# ---------------------------------------------------------------------------------------------------------------------
# script handlers (Z family)
# ---------------------------------------------------------------------------------------------------------------------
def op1B(e: Ent, lo: int, hi: int):
    e.forcez = s32((((hi << 8) | lo) ^ 0x8000) - 0x8000) << 8      # sext16 << 8


def op20(e: Ent, pc: int, lo: int, hi: int) -> int:
    if e.memo_pc != pc:
        e.memo_pc, e.memo = pc, e.posz
        return 0
    d = abs(e.memo - e.posz)
    return 3 if (d >> 16) >= (lo | hi << 8) else 0


def op22(e: Ent, pc: int, height: int) -> int:
    if e.memo_pc != pc:
        e.memo_pc, e.memo = pc, height << 19
        return 0
    if e.memo == e.posz:
        return 1
    delta = e.memo - e.posz
    if delta > 0:
        clamp = delta < e.forcez
    else:
        clamp = e.forcez < delta
    if clamp:
        e.forcez = delta
    return 0


def dll(x: int) -> int:
    return x - 1
