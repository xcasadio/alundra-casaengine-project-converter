"""oracle_p.py - the REAL physics pass P (0x80038364) of ALUN_CD.EXE (France), hero in slot 0, carriers / passengers in slots 1.., rider copy 0x80037364 NOT stubbed.

Built on zsim.py (archived copy in h2tools/): its Sim() builds the memory image (map header, flat cells, entity fields), rebuild_lists() runs the REAL list builder 0x800384F4.
This module adds what the carrier / passenger question needs and zsim lacks:
  * a hero in slot 0 (the hero branch of the force pass 0x80036828 is taken because the entity address equals 0x80127D30),
  * the counters pass 0x80038998 as the binary runs it before P (clears +0x12C of every slot; here re-implemented in 3 lines, the other counters do not matter),
  * physics() WITHOUT the stub of 0x80037364 that zsim.physics() installs,
  * snapshots of the fields read by the plan (PosZ, ForceZ, FinalForceZ, +0x12C, +0x140, +0x144, +0x134, +0x138, FinalForceX/Y, AdjustedForceX/Y, PosX/PosY).
Tick order reproduced (0x8003B388): script phase (here: python writes of ForceZ / XY forces by the caller) -> counters 0x80038998 -> lists 0x800384F4 -> (UpdateAnimation: none) -> P.
All Z values are 16.16 raw integers in the BINARY convention (rest on a terrain T is T + 1).  Never writes outside this folder.
"""
import sys, os
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "h2tools"))
import zsim
from zsim import Sim, ent, F, M32, s32, U, SENT, SP
from mips import Cpu

HERO_FLAGS = 0x03118C                     # Alundra: MoreFlags 140 | CanPickup 17 << 8 | FlagsPortraitShadowType 3 << 16
HERO_OFF, HERO_SIZE = (-10, -7, 0), (21, 15, 32)
PLAT_FLAGS_BIG = 0x0560C0                 # "Plateforme flottante (grande)": collidable 0x80, no gravity
PLAT_OFF_BIG, PLAT_SIZE_BIG = (-24, -16, 0), (48, 32, 8)
PLAT_FLAGS_SMALL = 0x0360C0
PLAT_OFF_SMALL, PLAT_SIZE_SMALL = (-12, -8, 0), (24, 16, 8)

OFF_FX, OFF_FY = 0xE4, 0xE8               # FinalForceX / Y
OFF_AX, OFF_AY = 0xDC, 0xE0               # AdjustedForceX / Y (after the map-bound clamp 0x800366FC)
OFF_RIDE = 0x12C
OFF_FORCEX_TARGET, OFF_FORCEY_TARGET = 0xBC, 0xC0
OFF_FORCEX, OFF_FORCEY = 0xC4, 0xC8


class P:
    def __init__(self, gravity=128, visc=4096, cells=None):
        self.s = Sim(gravity=gravity, visc=visc, cells=cells or (lambda c, r: (0, 0, 0, 0)))
        self.m = self.s.mem
        self.calls = []

    # ---- set-up -----------------------------------------------------------------------------------------------------
    def add(self, idx, flags, off, size, pos, status=2, init=True):
        self.s.add(idx, flags, off=off, size=size, pos=pos, status=status)
        if init:
            self.s.init_state(idx, clamp=False)
        return ent(idx)

    def get(self, idx, name):
        return self.s.get(idx, name)

    def put(self, idx, name, v):
        self.s.put(idx, name, v)

    def rw(self, idx, off):
        return s32(self.m.rw(ent(idx) + off))

    def ww(self, idx, off, v):
        self.m.ww(ent(idx) + off, v & M32)

    # ---- one tick ---------------------------------------------------------------------------------------------------
    def counters(self):
        """0x80038998: +0x12C (RidingEntity) of every slot 0..max is cleared (0x800389DC: sw $zero, -0xa4($v1) with $v1 = slot + 0x1D0)."""
        for i in range(self.s.n):
            self.ww(i, OFF_RIDE, 0)

    def physics(self, rider_copy=True):
        m = self.m
        m.ww(SP + 0x1C, SENT)
        cpu = Cpu(m, {} if rider_copy else {0x80037364: 0})
        cpu.r[29] = SP
        cpu.run(0x80038364, SENT, max_steps=300_000)
        self.calls = cpu.calls
        return cpu

    def tick(self, rider_copy=True):
        self.counters()
        self.s.rebuild_lists()
        return self.physics(rider_copy)

    def snap(self, idx):
        g = lambda n: self.get(idx, n)
        ride = self.rw(idx, OFF_RIDE) & M32
        ride_slot = None
        if ride:
            ride_slot = (ride - zsim.ENT0) // zsim.STRIDE
        return dict(x=self.rw(idx, 0x114), y=self.rw(idx, 0x118), z=g("posz"), fz=g("forcez"), ffz=g("finalz"), ride=ride_slot, coll=g("coll"),
                    ground=g("ground"), floor=g("floor"), terr=g("terrain"), adj=g("adj"), ffx=self.rw(idx, OFF_FX), ffy=self.rw(idx, OFF_FY),
                    afx=self.rw(idx, OFF_AX), afy=self.rw(idx, OFF_AY))


def flat(h=0):
    return lambda c, r: (h, 0, 0, 0)
