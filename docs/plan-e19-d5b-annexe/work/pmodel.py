"""pmodel.py - pure-python model of the BINARY's carrier / passenger update (Z axis, XY forces optional) = the physics pass P 0x80038364, binary convention.

Passes modelled (addresses of ALUN_CD.EXE (France)):
  counters   0x80038998   +0x12C (RidingEntity) of every slot := 0 (done before P by the tick)
  lists      0x800384F4   active list: status 2 or 3 and +0x20 == 0 ; collidable list: flags & 0x80, AnimFlags(+0xB4) & 0x80 == 0, +0x28 == 0 (NO status test)
  prepass    0x80038364   active entities: moved(+0x168) := 0, CollidedWithEntityZ(+0x140) := 0, ForceAdjusted(+0x13C) := 0, Modded := Pos + Mod
  riders     0x800364C8   collidable list order, self with (flags & 0x4100) == 0x100: the FIRST other collidable whose top + 1 == self.ModdedZ and whose XY box overlaps
                          (window: dx >= 0 ? dx < self.W + 1 : -dx < other.W + 1 ; same on Y with H) becomes self.RidingEntity
  forces     0x80036828   active list order: attached -> zero ; impulse -> (low16 == 0x8000 and no gravity ? 0 : IZF << 8) ; else gravity -> decay, two-sided clamp ; XY (optional) ;
                          FinalForceZ := ForceZ ; FinalForceX/Y := AdjustedForceX/Y
  ridercopy  0x80037364   collidable list order, entity with RidingEntity != 0: first the carrier's own copy (recursion), then FinalForceX/Y += carrier.AdjustedForceX/Y,
                          and, if the passenger has no pending impulse, ForceZ := FinalForceZ := carrier.FinalForceZ
  moves      0x80038364   active list order: if !moved -> MoveEntity 0x80037E34 = ComputeZ 0x800375E0 then ComputeXY 0x80037730 (XY part: only the Modded refresh when the XY force is 0)
  ComputeZ   F > 0: ceiling 0x80036D94 ; F <= 0: landing 0x80036BFC ; a hit on an entity that has NOT moved yet moves it first (MoveEntity recursion) and re-tests
  endpass    0x80038064   FloorHeight 0x80037F28, IsOnGround := !(Floor < PosZ)
Everything is integers (16.16).  This module has NO dependency on the interpreter: it is compared with the real code by oracle_p.py / vec_p.py.
"""
from dataclasses import dataclass, field
from typing import Optional, List

CAP = 0x7800000


def s32(v):
    v &= 0xFFFFFFFF
    return v - (1 << 32) if v & 0x80000000 else v


@dataclass
class E:
    idx: int
    flags: int
    animflags: int = 0
    status: int = 2
    blocked: int = 0              # +0x20
    attached: Optional[int] = None  # +0x28 (index of the carrier entity) ; modelled only as "excluded from the lists, zero forces"
    posx: int = 0
    posy: int = 0
    posz: int = 0
    modx: int = 0
    mody: int = 0
    modz: int = 0
    w: int = 0                    # +0x1F0 = (SizeX << 16) - 1 or 0
    h: int = 0
    d: int = 0
    forcez: int = 0
    finalz: int = 0
    izf: int = 0
    # XY (optional)
    adjx: int = 0                 # +0xDC
    adjy: int = 0                 # +0xE0
    finx: int = 0                 # +0xE4
    finy: int = 0                 # +0xE8
    # derived / state
    mx: int = 0
    my: int = 0
    mz: int = 0
    ride: Optional[int] = None    # +0x12C (index)
    moved: int = 0
    coll: int = 0
    adjflag: int = 0
    ground: int = 0
    floor: int = 0
    terrain: int = 0

    def active(self):
        return self.status in (2, 3) and self.blocked == 0

    def collidable(self):
        return (self.flags & 0x80) != 0 and (self.animflags & 0x80) == 0 and self.attached is None

    def subject(self):
        return (self.flags & 0x80) != 0 and (self.animflags & 0x80) == 0 and self.attached is None

    def gravity(self):
        return (self.flags & 0x100) != 0


class World:
    """entities in slot order, flat terrain T (binary 16.16 height of the probe), map gravity G and viscosity V"""

    def __init__(self, ents: List[E], T=0, G=128, V=4096, mut=frozenset()):
        self.e = ents
        self.T = T
        self.G, self.V = G, V
        self.mut = mut
        for x in self.e:
            x.mx, x.my, x.mz = x.posx + x.modx, x.posy + x.mody, x.posz + x.modz
            x.terrain = T

    # ---------------------------------------------------------------- lists
    def lists(self):
        self.act = [x for x in self.e if x.active()]
        self.col = [x for x in self.e if x.collidable()]

    # ---------------------------------------------------------------- XY overlap used by riders, landing, ceiling, floor
    def overlap(self, me: E, c: E):
        """the half-open XY test of the binary (me = the searching entity, c = the candidate)"""
        k = 0 if "win_no_plus1" in self.mut else 1
        dx = c.mx - me.mx
        if dx >= 0:
            if not (dx < me.w + k):
                return False
        else:
            if not (me.mx - c.mx < c.w + k):
                return False
        dy = c.my - me.my
        if dy >= 0:
            if not (dy < me.h + k):
                return False
        else:
            if not (me.my - c.my < c.h + k):
                return False
        return True

    # ---------------------------------------------------------------- one full pass (counters, lists, P)
    def tick(self, xy=False):
        self._calls = 0
        for x in self.e:          # 0x80038998
            x.ride = None
        self.lists()
        self.P(xy)

    def P(self, xy=False):
        # prepass
        for x in self.act:
            x.moved = 0
            x.coll = 0
            x.adjflag = 0
            x.mx, x.my, x.mz = x.posx + x.modx, x.posy + x.mody, x.posz + x.modz
        self.riders()
        self.forces(xy)
        if "no_ridercopy" not in self.mut:
            for x in self.col:
                if x.ride is not None:
                    self.ridercopy(x, self.e[x.ride])
        for x in self.act:
            if x.moved == 0:
                self.move(x)
        for x in self.act:
            self.endpass(x)

    # ---------------------------------------------------------------- 0x800364C8
    def riders(self):
        for me in self.col:
            if ((me.flags & 0x100) != 0x100) if "ride_mask_0x100" in self.mut else ((me.flags & 0x4100) != 0x100):
                continue
            for c in self.col:
                if c is me:
                    continue
                top1 = c.mz + c.d + (0 if "ride_top_nopl1" in self.mut else 1)
                if top1 != me.mz:
                    continue
                if self.overlap(me, c):
                    me.ride = c.idx
                    break

    # ---------------------------------------------------------------- 0x80036828 (Z + optional XY)
    def forces(self, xy):
        for x in self.act:
            if x.attached is not None:
                x.forcez = 0
                x.finalz = 0
                x.adjx = x.adjy = x.finx = x.finy = 0
                continue
            if x.izf != 0:
                # hero branch (slot 0, 0x80036894-0x800368E8): IZF << 8 always (x160 water case needs +0x180 & 0x10: not modelled) ; other entities: the stop marker 0x8000 without gravity gives 0
                if x.idx != 0 and (x.izf & 0xFFFF) == 0x8000 and not x.gravity():
                    x.forcez = 0
                else:
                    x.forcez = s32(x.izf << 8)
            elif x.gravity():
                v = x.forcez - (self.G << 8)
                lim = self.V << 8
                if "one_sided" in self.mut:
                    if v < -lim:
                        v = -lim
                elif abs(v) > lim:
                    v = lim if v > 0 else -lim
                x.forcez = v
            x.finalz = x.forcez
            if xy:
                x.finx, x.finy = x.adjx, x.adjy       # (the XY target is set by the caller as adjx / adjy)
            else:
                x.adjx = x.adjy = x.finx = x.finy = 0

    # ---------------------------------------------------------------- 0x80037364
    def ridercopy(self, x: E, carrier: E):
        if carrier.ride is not None and "no_recursion" not in self.mut:
            self.ridercopy(carrier, self.e[carrier.ride])
        x.finx += carrier.adjx
        x.finy += carrier.adjy
        if x.izf == 0 or "copy_ignores_impulse" in self.mut:
            x.forcez = carrier.finalz
            x.finalz = carrier.finalz

    # ---------------------------------------------------------------- 0x80037E34
    def move(self, x: E):
        self._depth = getattr(self, "_depth", 0) + 1
        self._calls = getattr(self, "_calls", 0) + 1
        try:
            if self._depth > 8 or self._calls > 400:
                raise RuntimeError("MoveEntity recursion without end")
            self._move(x)
        finally:
            self._depth -= 1

    def _move(self, x: E):
        x.moved = 1
        if x.attached is not None:       # attached object: out of the model (the binary copies its carrier)
            return
        self.compute_z(x)
        # ComputeXY: no XY force -> only the Modded refresh and the terrain re-probe (0x80037DC0)
        if x.finx == 0 and x.finy == 0:
            x.mx, x.my, x.mz = x.posx + x.modx, x.posy + x.mody, x.posz + x.modz
            x.terrain = self.T
        else:
            x.posx += x.finx
            x.posy += x.finy
            x.mx, x.my, x.mz = x.posx + x.modx, x.posy + x.mody, x.posz + x.modz
            x.terrain = self.T

    # ---------------------------------------------------------------- 0x800375E0
    def compute_z(self, x: E):
        F = x.finalz
        x.terrain = self.T
        if F > 0:
            while True:
                hit, t, other = self.ceiling(x)
                if not hit:
                    x.posz += F
                    return
                if other is not None and (other.moved == 0 or "moved_ignored" in self.mut) and "no_move_first" not in self.mut:
                    self.move(other)
                    continue
                x.coll = 1
                x.posz = t - x.modz - x.d
                if x.gravity():
                    x.forcez = 0
                return
        else:
            while True:
                landed, t1, other = self.landing(x)
                if not landed:
                    x.posz += F
                    return
                if other is not None and other.moved == 0 and "no_move_first" not in self.mut:
                    self.move(other)
                    continue
                x.coll = 1
                x.posz = t1 - x.modz
                if x.gravity():
                    x.forcez = 0
                return

    # ---------------------------------------------------------------- 0x80036BFC
    def landing(self, x: E):
        t1 = x.mz + x.finalz
        t6 = 0
        t7 = None
        T = x.terrain
        if (not (T <= t1)) if "land_t_ge" in self.mut else (not (T < t1)):
            t1 = T + 1
            t6 = 1
        if x.subject():
            for c in self.col:
                if c is x:
                    continue
                top = c.mz + c.d
                if (not (top <= x.mz)) if "land_cand_le" in self.mut else (not (top < x.mz)):
                    continue
                if (top <= t1) if "land_below_le" in self.mut else (top < t1):
                    continue
                if not self.overlap(x, c):
                    continue
                t1 = top + 1
                t7 = c
                t6 = 1
        return t6, t1, t7

    # ---------------------------------------------------------------- 0x80036D94
    def ceiling(self, x: E):
        top = x.mz + x.d
        t2 = top + x.finalz
        t7 = 0
        t8 = None
        if CAP < t2:
            t2 = 0x77FFFFF
            t7 = 1
        if x.subject():
            for c in self.col:
                if c is x:
                    continue
                t0 = c.mz
                if not (top < t0):
                    continue
                if (t2 <= t0) if "ceil_le" in self.mut else (t2 < t0):
                    continue
                if not self.overlap(x, c):
                    continue
                t2 = t0 - 1
                t8 = c
                t7 = 1
        return t7, t2, t8

    # ---------------------------------------------------------------- 0x80037F28 / 0x80038064
    def floor_height(self, x: E):
        t3 = x.terrain + 1
        if x.subject():
            for c in self.col:
                if c is x:
                    continue
                a2 = c.mz + c.d
                if not (a2 < x.mz):
                    continue
                if a2 < t3:
                    continue
                if self.overlap(x, c):
                    t3 = a2 + 1
        return t3

    def endpass(self, x: E):
        x.floor = self.floor_height(x)
        x.ground = 0 if x.floor < x.posz else 1

    def init_endpass(self):
        """InitializeEntity's end pass (0x80038064 via init_state) runs for EVERY entity added, inactive ones included"""
        self.lists()
        for x in self.e:
            self.endpass(x)

    # ---------------------------------------------------------------- snapshot
    def snap(self, x: E):
        return dict(z=x.posz, fz=x.forcez, ffz=x.finalz, ride=x.ride, coll=x.coll, ground=x.ground, floor=x.floor, terr=x.terrain, mz=x.mz,
                    x=x.posx, y=x.posy, ffx=x.finx, ffy=x.finy)
