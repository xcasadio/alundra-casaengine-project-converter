"""dllmodel.py - python TRANSCRIPTION (not an execution) of what the DLL does today to a hero carried by a script-driven carrier, Z axis only, and of the proposed order.
Source of each rule: Alundra/Scripts at HEAD 2010ec3 (read-only reading).  Tag: INFERENCE (the DLL was not run).  DLL convention (ADR-0026): PosZ_dll = PosZ_binary - 1.

Two orders, same rules, so that only the ORDER differs:
  order="current"  : AlundraWorldProxy.RunEntityBlock (2871-2954): programs (the caller writes the carrier's ForceZ) -> counters (RidingEntity kept) -> lists
                     (RefreshUpdateProxiesAndCollidables 3641 -> EntitySupport.UpdateRidingEntities) -> physics loop hero first (TickPhysics 1589): hero =
                     RunOneMotionTick (AlundraScriptedMotion.cs:180-227: UpdateRidingEntityOfHero, RunHeroVerticalTick 245-394, IsOnGround from ComputeFloorHeight 223) ;
                     carrier = TickScriptedNpc + EvaluateEntitySupport (AlundraEntityScriptProxy.cs:726-1031).
  order="proposed" : forces pass (every entity's tick force, once), rider copy, moves in slot order with 'move the unmoved target first' on a landing / ceiling hit, end pass
                     (IsOnGround / FloorHeight of every entity after all moves) ; RidingEntity cleared by the counters pass and rebuilt by the lists step (unchanged code).
"""
from dataclasses import dataclass
from typing import Optional

U = 65536


def s32(v):
    v &= 0xFFFFFFFF
    return v - (1 << 32) if v & 0x80000000 else v


@dataclass
class D:
    name: str
    hero: bool
    gravity: bool
    posz: int
    modz: int = 0
    depth: int = 0
    w: int = 0          # Width / Height of the logical box (DLL fields: size << 16 - 1)
    h: int = 0
    mx: int = 0         # PosX + ModX, PosY + ModY (XY fixed in these scenarios: full overlap)
    my: int = 0
    forcez: int = 0
    finalz: int = 0
    tickforce: int = 0  # TickForceZ
    izf: int = 0
    ride: Optional[object] = None
    coll: int = 0
    ground: int = 0
    held: bool = False  # hero: HeroAirborne (the state the tick holds)
    moved: bool = False
    collidable: bool = True


class DW:
    def __init__(self, ents, terrain=0, G=128, V=4096, order="current", ablate=frozenset()):
        self.ablate = ablate        # subset of {"peek", "movefirst", "endpass"} : the proposed order WITHOUT that mechanism
        self.e = ents
        self.T = terrain          # DLL terrain height under every box (flat)
        self.G, self.V = G, V
        self.order = order

    # ---- EntitySupport -----------------------------------------------------------------------------------------------------------
    def top(self, c):
        return c.posz + c.modz + c.depth

    def overlap(self, me, c):
        dx = c.mx - me.mx
        ok_x = (me.mx - c.mx < c.w + 1) if dx < 0 else (dx < me.w + 1)
        dy = c.my - me.my
        ok_y = (me.my - c.my < c.h + 1) if dy < 0 else (dy < me.h + 1)
        return ok_x and ok_y

    def find_riding(self, me):
        """EntitySupport.FindRidingEntity (EntitySupport.cs:310-366): first other collidable with top + 1 == my ModdedPosZ and an XY overlap"""
        if not me.gravity:
            return None
        for c in self.e:
            if c is me or not c.collidable:
                continue
            if self.top(c) + 1 != me.posz + me.modz:
                continue
            if self.overlap(me, c):
                return c
        return None

    def try_find_support(self, me, seed):
        """EntitySupport.TryFindSupport (EntitySupport.cs:108-182): highest candidate strictly below the feet and at/above the seed"""
        mz = me.posz + me.modz
        platform_top = seed
        found = None
        sup_top = 0
        for c in self.e:
            if c is me or not c.collidable:
                continue
            ctop = self.top(c)
            if ctop >= mz or platform_top > ctop:
                continue
            if not self.overlap(me, c):
                continue
            found = c
            platform_top = ctop + 1
            sup_top = platform_top
        return found, sup_top

    def floor_height(self, me):
        """AlundraEntityScriptProxy.ComputeFloorHeight (2192-2200): TryFindSupport seeded at terrain + 1, else terrain"""
        c, top = self.try_find_support(me, self.T + 1)
        return top if c is not None else self.T

    # ---- the vertical law of a non-hero entity (ApplyVerticalForceLaw 1040-1065) ------------------------------------------------------
    def law(self, x):
        if x.izf != 0:
            f = 0 if ((x.izf & 0xFFFF) == 0x8000 and not x.gravity) else x.izf << 8
            return f
        if x.gravity:
            f = x.forcez - (self.G << 8)
            lim = self.V << 8
            return max(-lim, min(lim, f))
        return x.forcez

    # ---- one tick ------------------------------------------------------------------------------------------------------------------
    def tick(self):
        for x in self.e:
            x.moved = False
            x.coll = 0
        # counters: current keeps RidingEntity (AlundraWorldProxy.cs:2966-2989), proposed clears it ; lists recompute for gravity collidables in both
        if self.order == "proposed":
            for x in self.e:
                x.ride = None
        for x in self.e:
            if x.collidable and x.gravity:
                x.ride = self.find_riding(x)
        if self.order == "current":
            for x in self.e:         # slot order, hero first
                self.tick_entity_current(x)
        else:
            self.tick_proposed()

    # ----------------------------------------------------------------- CURRENT
    def tick_entity_current(self, x):
        if x.hero:
            self.hero_step(x, copy_from="stale")
            # RunHeroProbes -> UpdateFloorHeight ; IsOnGround written by RunOneMotionTick:223 right after the vertical step (platform NOT yet moved)
            x.ground = 1 if (x.posz + x.modz <= self.floor_height(x)) else 0
        else:
            self.carrier_step(x)

    def carrier_step(self, c):
        """TickScriptedNpc (FinalForceZ := ForceZ, RunOneKinematicTick:668) then EvaluateEntitySupport (ApplyVerticalForceLaw, TickForceZ, terrain landing / controller Move)"""
        c.finalz = c.forcez
        f = self.law(c)
        if c.izf != 0 or c.gravity:
            c.forcez = f
            c.finalz = f
        c.tickforce = c.finalz
        mz = c.posz + c.modz
        found, top = (None, 0)
        if c.collidable:
            found, top = self.try_find_support(c, max(mz + c.finalz, self.T + 1))
        if found is not None:
            c.coll = 1
            c.posz = top - c.modz
            if c.gravity:
                c.forcez = 0
            c.ground = 1
            return
        if c.finalz < 1 and mz + c.finalz <= self.T + 1 - 1:
            c.posz = self.T - c.modz
            if mz + c.tickforce < self.T:
                c.coll = 1
            if c.gravity:
                c.forcez = 0
                c.finalz = 0
            c.ground = 1
        else:
            c.posz += c.finalz
            c.ground = 0
        c.moved = True

    def hero_step(self, hero, copy_from):
        """RunHeroVerticalTick (AlundraScriptedMotion.cs:245-394) in the held state (hero.held), without impulse / ceiling above"""
        impulse = hero.izf != 0
        if impulse:
            hero.forcez = hero.izf << 8
        elif hero.gravity:
            f = hero.forcez - (self.G << 8)
            lim = self.V << 8
            hero.forcez = max(-lim, min(lim, f))
        carrier = hero.ride
        if not impulse and carrier is not None:
            if copy_from == "stale":
                hero.forcez = carrier.forcez            # carrier.ForceZ (live)
                hero.finalz = carrier.tickforce         # carrier.TickForceZ (the carrier's PREVIOUS tick)
            else:
                hero.forcez = carrier.tickforce
                hero.finalz = carrier.tickforce
        else:
            hero.finalz = hero.forcez
        F = hero.finalz
        landing = self.T
        on_entity = False
        if F <= 0:
            c, top = self.try_find_support(hero, max(hero.posz + hero.modz + F, self.T + 1))
            if c is not None and copy_from == "moved_first" and "movefirst" not in self.ablate:
                if not c.moved:
                    self.carrier_step_forces_done(c)        # the binary's MoveEntity recursion (0x800376D0)
                c2, top2 = self.try_find_support(hero, max(hero.posz + hero.modz + F, self.T + 1))
                c, top = c2, top2
            if c is not None:
                landing, on_entity = top, True
        if F <= 0 and hero.posz + hero.modz + F < landing:
            hero.posz = landing - hero.modz
            if hero.gravity:
                hero.forcez = 0
                hero.finalz = 0
            hero.coll = 1
        else:
            hero.posz += F
        hero.moved = True
        if "endpass" in self.ablate:
            hero.ground = 1 if (hero.posz + hero.modz <= self.floor_height(hero)) else 0     # IsOnGround right after the hero's own step, as today

    # ----------------------------------------------------------------- PROPOSED
    def tick_proposed(self):
        # forces pass: every entity's tick force (once)
        for x in self.e:
            if x.hero or "peek" in self.ablate:
                continue
            f = self.law(x)
            if x.izf != 0 or x.gravity:
                x.forcez = f
            x.finalz = x.forcez if not (x.izf != 0 or x.gravity) else f
            x.tickforce = x.finalz
        # rider copy is inside hero_step (copy_from='moved_first' reads carrier.tickforce, now of THIS tick) ; moves in slot order, unmoved target first
        for x in self.e:
            if x.moved:
                continue
            if x.hero:
                self.hero_step(x, copy_from="moved_first")
            else:
                self.carrier_step_forces_done(x)
        # end pass: IsOnGround after every entity moved
        for x in self.e:
            if x.hero and "endpass" not in self.ablate:
                x.ground = 1 if (x.posz + x.modz <= self.floor_height(x)) else 0

    def carrier_step_forces_done(self, c):
        """as carrier_step but the force of the tick was computed by the forces pass (no second decay)"""
        if "peek" in self.ablate:                     # without the peek the carrier computes its own force at its own step, as today (the hero read the previous tick's)
            c.finalz = c.forcez
            f = self.law(c)
            if c.izf != 0 or c.gravity:
                c.forcez = f
                c.finalz = f
            c.tickforce = c.finalz
        mz = c.posz + c.modz
        found, top = (None, 0)
        if c.collidable:
            found, top = self.try_find_support(c, max(mz + c.finalz, self.T + 1))
        if c.finalz > 0 and getattr(self, 'carrier_ceiling', False):
            ctop = mz + c.depth
            target = ctop + c.finalz
            hit = None
            for o in self.e:
                if o is c or not o.collidable:
                    continue
                oz = o.posz + o.modz
                if not (ctop < oz and oz <= target):
                    continue
                if not self.overlap(c, o):
                    continue
                target = oz - 1       # binary: t0 - 1 (binary conv.) == oz - 1 (DLL conv.)
                hit = o
            if hit is not None:
                c.posz = target - c.depth - c.modz
                c.coll = 1
                c.ground = 0
                c.moved = True
                return
        if found is not None:
            c.coll = 1
            c.posz = top - c.modz
            if c.gravity:
                c.forcez = 0
            c.ground = 1
        elif c.finalz < 1 and mz + c.finalz <= self.T:
            c.posz = self.T - c.modz
            if mz + c.tickforce < self.T:
                c.coll = 1
            if c.gravity:
                c.forcez = 0
                c.finalz = 0
            c.ground = 1
        else:
            c.posz += c.finalz
            c.ground = 0
        c.moved = True
