"""Checker's own emulation of the ALUN_CD.EXE (France) per-tick entity loop, written from the checker's disassembly
(mdis.py listings: 0x8003B388 UpdateEntities, 0x800386D0 events, 0x800384F4 lists, 0x80038364 physics,
0x80036828/0x80036614/0x800367E4/0x800366FC forces, 0x80037E34 MoveEntity, 0x800375E0 ComputeZ (simplified),
0x80037730 ComputeXYPosition, 0x80036F34 FindEntityCollisionCandidate, 0x800370C4 ground height, 0x800373E4 /
0x80037488 tile tests, 0x8003D468 0x0B, 0x8007ED10 native E handler, 0x80038634 destroyed-slot recycling).

Not a game observation: a computation. Simplifications (stated): flat cells only (slope&3 must be 0 on visited cells,
counted), no tile slide forces (slope&0xC0 must be 0, counted), Z fall = constant gravity guess, animation clock
approximated for 0x1C, dialogs = fixed tick count, map events other than the scene program not run.

Options (World(...)):
  entity_collision  - FECC called in ComputeXYPosition (binary True)
  destroy_on_deact  - native E handler 0/1 destroys a Deactivated entity (binary True; DLL today False)
  recycle_destroyed - FlagToDestroy slots leave the lists at the next tick (binary True; DLL today False)
  border_clamp      - ApplyEntityForces clamp to the 1248 x 960 grid (binary True; DLL today False)
"""
import json
import sys

import mlib

U = 65536
OX = [0, -150, -294, -422, -543, -639, -710, -753, -768, -753, -710, -639, -543, -422, -294, -150,
      0, 150, 294, 422, 543, 639, 710, 753, 768, 753, 710, 639, 543, 422, 294, 150]
OY = [512, 502, 473, 426, 362, 284, 196, 100, 0, -100, -196, -284, -362, -426, -473, -502,
      -512, -502, -473, -426, -362, -284, -196, -100, 0, 100, 196, 284, 362, 426, 473, 502]
HERO_ID = "192c2eeb-bdda-5bb4-92d6-39a4ae298307"
G_ACC = 0x8000      # gravity guess per tick (only used when an entity is airborne)
G_MAX = 8 * U
SLOPE_T = [1, 2, 2, 3, 4, 4, 5, 6, 6, 7, 8, 8, 9, 10, 10, 11, 12, 12, 13, 14, 14, 15, 16, 16]  # 0x800236D4


def sra(v, n=1):
    return v >> n  # python >> on negative ints is arithmetic


def inc_force(force, target, step):  # 0x800367E4
    if force == target:
        return target
    if force < target:
        force += step
        return target if target < force else force
    force -= step
    return target if force < target else force


class Ent:
    def __init__(self, name, x, y, z, hdr, player=False, rec=None):
        self.name = name
        self.rec = rec
        self.player = player
        self.PosX, self.PosY, self.PosZ = int(round(x * U)), int(round(y * U)), z
        ox, oy, oz, sx, sy, sz = hdr["OffsetX"], hdr["OffsetY"], hdr["OffsetZ"], hdr["SizeX"], hdr["SizeY"], hdr["SizeZ"]
        self.ModX, self.ModY, self.ModZ = ox << 16, oy << 16, oz << 16
        self.W = sx * U - 1 if sx else 0
        self.H = sy * U - 1 if sy else 0
        self.D = sz * U - 1 if sz else 0
        self.minX = -(ox << 16)
        self.minY = -(oy << 16)
        self.maxX = 0x4E00000 - ((ox + sx) << 16)
        self.maxY = 0x3C00000 - ((oy + sy) << 16)
        self.Flags = hdr["MoreFlags"] | hdr["CanPickup"] << 8 | hdr["FlagsPortraitShadowType"] << 16
        self.anims = {a["Anim"]: (a["Speed"], a["Acceleration"]) for a in hdr["AnimSets"]}
        self.frames = {}
        for a in hdr.get("IdsvAnimDirs", []):
            self.frames[(a["Anim"], a["Direction"])] = (a["Frames"], a["End"], a.get("ChainTo"))
        self.TargetAnim = 0
        self.CurAnim = None
        self.AnimFlags = 0
        self.TargetDir = 0
        self.CurDirF = 0   # +0x94
        self.SpeedF = 0    # +0xF4
        self.AccF = 0      # +0xF0
        self.ForceX = self.ForceY = 0
        self.TFX = self.TFY = 0
        self.StepX = self.StepY = 0
        self.ForceZ = 0
        self.FinalX = self.FinalY = 0
        self.FA = 0
        self.status = 1
        self.ret130 = None
        self.carried = False
        self.refresh()
        # animation clock (approximation for 0x1C)
        self.anim_delay = 0
        self.anim_frame = 0
        self.hold = 0
        self.complete = 0
        self.animdir = None
        # script
        self.prog = None
        self.pc = None
        self.state = {}
        self.log = []
        self.last_contact = None
        self.player_extra_hits = 0
        self.slope_cells = 0

    def refresh(self):
        self.MX = self.PosX + self.ModX
        self.MY = self.PosY + self.ModY
        self.MZ = self.PosZ + self.ModZ

    def px(self):
        return self.PosX / U, self.PosY / U

    def collidable(self):
        return (self.Flags & 0x80) and not (self.AnimFlags & 0x80) and not self.carried


class World:
    def __init__(self, mid, entity_collision=True, destroy_on_deact=True, recycle_destroyed=True, border_clamp=True,
                 dialog_ticks=60):
        self.mp = mlib.Map(mid)
        c = self.mp.cells
        self.Wc, self.Hc = self.mp.W, self.mp.H
        self.walk, self.gp, self.slope, self.height = c["walkability"], c["ground_property"], c["slope"], c["height"]
        self.ents = []        # slot order
        self.hero = None
        self.flags = set()
        self.tick = 0
        self.entity_collision = entity_collision
        self.destroy_on_deact = destroy_on_deact
        self.recycle_destroyed = recycle_destroyed
        self.border_clamp = border_clamp
        self.dialog_ticks = dialog_ticks
        self.SR = mlib.sprite_records()
        self.recs = {int(o["custom_properties"]["Index"]): o["custom_properties"] for o in self.mp.layers["Entities"]}
        self.events = []
        self.map_prog = None   # (pc, state) for the scene's map program, logic entity = hero
        self.bad_slope = 0   # corners sampled on slope cells (now computed as 0x800370C4 does)
        self.force_tiles = 0  # corners on slope&0xC0 cells (tile forces NOT emulated)
        self.anim_lag = False   # True: emulate the DLL's kept one-tick CurrentAnimationId lag for NPC motion (D-E19-13)

    # ---------------------------------------------------------------- terrain
    def cell(self, xp, yp):
        col = xp // 24
        col = 0 if xp <= 0 else min(col, 51)
        row = yp >> 4
        row = 0 if row <= 0 else min(row, 59)
        return row * self.Wc + col

    def corners(self, e):
        x1 = e.MX >> 16
        x2 = (e.MX + e.W) >> 16
        y1 = e.MY >> 16
        y2 = (e.MY + e.H) >> 16
        out = []
        hits = 0  # a2 of 0x800370C4: slope kinds already met on an earlier corner
        for (x, y) in ((x1, y1), (x2, y1), (x1, y2), (x2, y2)):
            i = self.cell(x, y)
            t = self.slope[i] & 3
            h = self.height[i]
            if t == 0:
                a1 = h * 16
            else:
                a1 = (h - 1) * 16
                if t == 1:
                    if hits & 6:
                        a1 += 16
                    else:
                        a1 = a1 + 16 - (y - (int(y / 16) * 16))   # C-style remainder, as 0x8003723C
                        hits |= 1
                elif t == 2:
                    if hits & 5:
                        a1 += 16
                    else:
                        r = x - int(x / 24) * 24
                        a1 += SLOPE_T[23 - r]
                        hits |= 2
                else:
                    if hits & 3:
                        a1 += 16
                    else:
                        r = x - int(x / 24) * 24
                        a1 += SLOPE_T[r]
                        hits |= 4
                self.bad_slope += 1
            if self.slope[i] & 0xC0:
                self.force_tiles += 1
            out.append((i, a1 << 16))
        return out

    def ground(self, e):
        cs = self.corners(e)
        e._corners = cs
        return max(0, max(h for _, h in cs))

    def tile_blocked(self, e):  # 0x800373E4 / 0x80037488
        mask = 0x41 if e.Flags & 8 else 0x40
        if e.Flags & 1:
            mask |= 0x1000
        fl = [0, 0, 0, 0]
        for k, (i, h) in enumerate(e._corners):
            t = self.walk[i] | (self.gp[i] << 8)
            if (t & mask) or h >= e.MZ:
                fl[k] = 1
            if e.player and (t & 0xE00) == 0x800:   # g_gravityFlag < 2 assumed
                if not fl[k]:
                    e.player_extra_hits += 1
                fl[k] = 1
        return fl

    # ---------------------------------------------------------------- entity vs entity (0x80036F34)
    def fecc(self, e):
        if not self.entity_collision:
            return None
        if not e.collidable():
            return None
        for c in self.collidables:
            if c is e:
                continue
            d = c.MX - e.MX
            if d >= 0:
                if not d < e.W + 1:
                    continue
            elif not (e.MX - c.MX) < c.W + 1:
                continue
            d = c.MY - e.MY
            if d >= 0:
                if not d < e.H + 1:
                    continue
            elif not (e.MY - c.MY) < c.H + 1:
                continue
            d = c.MZ - e.MZ
            if d >= 0:
                if d < e.D + 1:
                    return c
                continue
            if (e.MZ - c.MZ) < c.D + 1:
                return c
        return None

    # ---------------------------------------------------------------- 0x80037730
    def cxy(self, e):
        straight = (e.TargetDir & 7) == 0
        did_adjust = 0
        while True:
            s2, s1 = e.FinalX, e.FinalY
            if s2 == 0 and s1 == 0:
                e.refresh()
                self.ground(e)
                return None
            advanced = 0
            s4 = None
            s5 = 0
            ret = None
            while True:  # TRY
                sx, sy, sz = e.PosX, e.PosY, e.PosZ
                ret = s4
                corner = None
                e._cornerflags = [0, 0, 0, 0]  # sp+0x10..0x1C cleared at every TRY
                e.PosX += s2
                e.PosY += s1
                e.refresh()
                g = self.ground(e)
                blocked = False
                snapped_ok = False
                if (e.Flags & 0x100) and e.ForceZ == 0:
                    dz = g - e.MZ - 1
                    lim = 0x30000 if dz >= 0 else 0x30003
                    if abs(dz) < lim:
                        s3 = e.PosZ
                        e.PosZ = g + 1
                        e.refresh()
                        c = self.fecc(e)
                        if c is None:
                            snapped_ok = True
                        else:
                            e.PosZ = s3
                            e.refresh()
                if not snapped_ok:
                    s4 = self.fecc(e)
                    if s4 is not None:
                        blocked = True
                        e.last_contact = s4.name
                if not blocked:
                    corner = self.tile_blocked(e)
                    blocked = any(corner)
                    e._cornerflags = corner
                if blocked:
                    e.PosX, e.PosY, e.PosZ = sx, sy, sz
                    s2 = 0 if s2 == -1 else sra(s2)
                    s1 = 0 if s1 == -1 else sra(s1)
                    if straight:
                        if s2 != 0 or s1 != 0:
                            s5 += 1
                            continue
                    else:
                        if s2 != 0 and s1 != 0:
                            s5 += 1
                            continue
                    break  # DECIDE
                # free probe
                advanced = 1
                if s5 == 0:
                    return ret
                s2 = 0 if s2 == -1 else sra(s2)
                s1 = 0 if s1 == -1 else sra(s1)
                if straight:
                    if s2 != 0 or s1 != 0:
                        s5 += 1
                        continue
                else:
                    if s2 != 0 and s1 != 0:
                        s5 += 1
                        continue
                return ret
            # DECIDE
            if advanced:
                e.refresh()
                self.ground(e)
                return ret
            if did_adjust == 1 or (e.Flags & 0x2000) or s4 is not None or e.TargetDir >= 32:
                e.FA = 1
                e.refresh()
                self.ground(e)
                return ret
            did_adjust = 1
            c0, c1, c2, c3 = getattr(e, "_cornerflags", [0, 0, 0, 0])
            d = e.TargetDir
            if d == 0:
                if (c2 and c3) or c0 or c1:
                    e.FA = 1; e.refresh(); return ret
                e.FinalY = 0
                if c2:
                    e.FinalX = 0xC000
                elif c3:
                    e.FinalX = -0xC000
            elif d == 8:
                if (c0 and c2) or c1 or c3:
                    e.FA = 1; e.refresh(); return ret
                e.FinalX = 0
                if c0:
                    e.FinalY = 0x8000
                elif c2:
                    e.FinalY = -0x8000
            elif d == 16:
                if (c0 and c1) or c2 or c3:
                    e.FA = 1; e.refresh(); return ret
                e.FinalY = 0
                if c0:
                    e.FinalX = 0xC000
                elif c1:
                    e.FinalX = -0xC000
            elif d == 24:
                if (c1 and c3) or c0 or c2:
                    e.FA = 1; e.refresh(); return ret
                e.FinalX = 0
                if c1:
                    e.FinalY = 0x8000
                elif c3:
                    e.FinalY = -0x8000
            elif 1 <= d <= 7:
                if c0:
                    if c3:
                        e.FA = 1; e.refresh(); return ret
                    e.FinalX = 0
                elif c3:
                    e.FinalY = 0
            elif 9 <= d <= 15:
                if c1:
                    if c2:
                        e.FA = 1; e.refresh(); return ret
                    e.FinalY = 0
                elif c2:
                    e.FinalX = 0
            elif 17 <= d <= 23:
                if c0 and c3:
                    e.FA = 1; e.refresh(); return ret
                if c3:
                    e.FinalX = 0
                elif c0:
                    e.FinalY = 0
            else:
                if c1 and c2:
                    e.FA = 1; e.refresh(); return ret
                if c2:
                    e.FinalY = 0
                elif c1:
                    e.FinalX = 0
            # restart (0x80037798)

    # ---------------------------------------------------------------- forces (0x80036B08..0x80036BB8, non-player path used for all)
    def forces(self, e):
        if e.carried:
            return
        if e.Flags & 0x100:
            e.ForceZ = max(e.ForceZ - G_ACC, -G_MAX)
        a = e.CurAnim if e.CurAnim is not None else e.TargetAnim
        if self.anim_lag and not e.player:
            a = e.lag_anim if getattr(e, "lag_anim", None) is not None else 0   # D-E19-13: NPC motion keyed off last tick's anim
        sp, acc = e.anims.get(a, (0, 0))
        acc4 = acc & 0xF
        if sp != e.SpeedF or e.CurDirF != e.TargetDir:
            e.SpeedF = sp
            e.CurDirF = e.TargetDir
            e.TFX = OX[e.TargetDir] * sp
            e.TFY = OY[e.TargetDir] * sp
            e.AccF = acc4
            e.StepX = abs(e.TFX - e.ForceX) >> acc4
            e.StepY = abs(e.TFY - e.ForceY) >> acc4
        elif e.AccF != acc4:
            e.AccF = acc4
            e.StepX = abs(e.TFX - e.ForceX) >> acc4
            e.StepY = abs(e.TFY - e.ForceY) >> acc4
        e.ForceX = inc_force(e.ForceX, e.TFX, e.StepX)
        e.ForceY = inc_force(e.ForceY, e.TFY, e.StepY)
        ax, ay = e.ForceX, e.ForceY
        if self.border_clamp:
            nx = e.PosX + ax
            if nx < e.minX or nx > e.maxX:
                ax = (e.minX if nx < e.minX else e.maxX) - e.PosX
                e.FA = 1
            ny = e.PosY + ay
            if ny < e.minY or ny > e.maxY:
                ay = (e.minY if ny < e.minY else e.maxY) - e.PosY
                e.FA = 1
        e.FinalX, e.FinalY = ax, ay

    def compute_z(self, e):  # simplified 0x800375E0 (ForceZ <= 0 branch only)
        if not (e.Flags & 0x100):
            return
        e.refresh()
        g = self.ground(e)
        if e.PosZ + e.ForceZ <= g + 1:
            e.PosZ = g + 1
            e.ForceZ = 0
        else:
            e.PosZ += e.ForceZ
        e.refresh()

    # ---------------------------------------------------------------- animation (approximate clock for 0x1C)
    @staticmethod
    def anim_dir_index(d):
        return {0: 0, 16: 1, 8: 2, 24: 3}.get((d + 4) & 0x18, 0)

    def update_animation(self, e):
        adir = self.anim_dir_index(e.TargetDir)
        if e.CurAnim != e.TargetAnim or e.animdir != adir:
            e.CurAnim = e.TargetAnim
            e.animdir = adir
            e.complete = 0
            e.anim_frame = 0
            e.hold = 0
            e.AnimFlags = e.anims.get(e.CurAnim, (0, 0))[1]
            fr = e.frames.get((e.CurAnim, adir))
            e.anim_delay = (fr[0][0] if fr and fr[0] else 0)
            return
        if e.anim_delay > 0:
            e.anim_delay -= 1
            if e.anim_delay > 0:
                return
            fr = e.frames.get((e.CurAnim, adir))
            if not fr:
                return
            frames, end, chain = fr
            if e.anim_frame >= len(frames) - 1:
                if end == "Loop":
                    e.anim_frame = 0
                    e.complete += 1
                    e.anim_delay = frames[0]
                elif end == "Hold":
                    e.hold = 1
                    e.anim_delay = 0
                elif end == "Chain":
                    e.TargetAnim = chain
                    self.update_animation(e)
            else:
                e.anim_frame += 1
                e.anim_delay = frames[e.anim_frame]

    # ---------------------------------------------------------------- spawning
    def spawn(self, rec, x, y, zpx, direction=None, name=None, prefab=None, player=False):
        r = self.recs.get(rec) if rec is not None else None
        pid = prefab or r["PrefabAssetId"]
        hdr = self.SR[pid]
        nm = name or ("rec%d %s" % (rec, r["EntityName"].split(" ")[0]))
        e = Ent(nm, x, y, (zpx << 16) + 1, hdr, player=player, rec=rec)
        if direction is None:
            sd = int(r["SpriteDirection"]) if r else 0
            direction = [0, 16, 8, 24][sd & 3]  # assumption: cardinal table order Down, Up, Left, Right
        e.TargetDir = direction
        e.refresh()
        g = self.ground(e)
        if e.PosZ <= g + 1:
            e.PosZ = g + 1
            e.refresh()
        if r is not None:
            ai = int(r["EventCodesA_LoadIndex"]) & 0x7F
            ci = int(r["EventCodesC_TickIndex"]) & 0x7F
            e.progA = self.mp.T["A"][ai] if ai else None
            e.progC = self.mp.T["C"][ci] if ci else None
        else:
            e.progA = e.progC = None
        if r is not None and r["EntityName"].startswith("I74_Bouquet"):
            e.progC = None  # held-bouquet followers: non-collidable after their A program (0x63 mask 0xF9FF), not driven
        e.status = 1
        e.trigger = None
        self.ents.append(e)
        return e

    def find(self, rec):
        for e in self.ents:
            if e.rec == rec and e.status in (1, 2, 3):
                return e
        return None

    # ---------------------------------------------------------------- script interpreter (subset)
    def run_program(self, e, st, logic):
        """st: dict with pc and per-opcode memory; returns when suspended. logic = logic entity."""
        C = self.mp.C
        guard = 0
        while True:
            guard += 1
            if guard > 1024:
                return
            pc = st["pc"]
            if pc is None:
                return
            op = C[pc]
            sz = mlib.SZ.get(op) or 1
            v = C[pc:pc + 10] + [0] * 10
            L = st.get("logic", logic)
            if op == 0x00:
                st["pc"] = pc + 1
                return
            if op == 0x53:
                self.events.append((self.tick, e.name, "0x53 @%d change map" % pc, L.px()))
                st["reached53"] = pc
                st["pc"] = None
                return
            if op == 0xFF:
                st["pc"] = None
                return
            if op == 0x02:
                st["pc"] = pc + mlib.s16(v[1] | v[2] << 8); continue
            if op in (0x03, 0x04):
                want = 1 if op == 0x03 else 0
                st["pc"] = pc + mlib.s16(v[1] | v[2] << 8) if (st.get("result", 0) != 0) == bool(want) else pc + 3
                continue
            if op in (0x05, 0x06):
                f = mlib.flag(v[1], v[2])
                (self.flags.add if op == 0x05 else self.flags.discard)(f)
                self.events.append((self.tick, e.name, "flag %s %s" % ("on" if op == 5 else "off", f), L.px()))
                st["pc"] = pc + 3; continue
            if op in (0x30, 0x31):
                f = mlib.flag(v[1], v[2])
                on = f in self.flags
                jump = on if op == 0x30 else not on
                st["pc"] = pc + mlib.s16(v[3] | v[4] << 8) if jump else pc + 5
                continue
            if op == 0x33:
                ok = all(mlib.flag(v[1 + 2 * k], v[2 + 2 * k]) in self.flags for k in range(4))
                st["result"] = 1 if ok else 0
                st["pc"] = pc + 9; continue
            if op == 0x36:  # advance when flag SET
                if mlib.flag(v[1], v[2]) in self.flags:
                    st["pc"] = pc + 3; continue
                return
            if op == 0x35:  # advance when flag CLEAR
                if mlib.flag(v[1], v[2]) not in self.flags:
                    st["pc"] = pc + 3; continue
                return
            if op == 0x37:
                if st.get("wpc") != pc:
                    st["wpc"] = pc
                    st["wn"] = v[1]
                    return
                st["wn"] -= 1
                if st["wn"] <= 0:
                    st["wpc"] = None
                    st["pc"] = pc + 2; continue
                return
            if op in (0x0D, 0x5C, 0x39, 0x4C, 0x4D):
                if op == 0x39:
                    if st.get("dpc") != pc:
                        st["dpc"] = pc
                        st["dn"] = self.dialog_ticks
                        return
                    st["dn"] -= 1
                    if st["dn"] <= 0:
                        st["dpc"] = None
                        st["pc"] = pc + 1; continue
                    return
                st["pc"] = pc + sz; continue
            if op in (0x10, 0x11, 0x69, 0xAF, 0xA5, 0xA6, 0x82, 0x91, 0x90, 0xBD, 0x89):  # 0x89: only bouquets' A programs here
                st["pc"] = pc + sz; continue
            if op == 0x09:
                L.TargetDir = v[1] & 0x1F
                st["pc"] = pc + 2; continue
            if op == 0x1A:
                L.TargetAnim = v[1]
                st["pc"] = pc + 2; continue
            if op == 0x59:
                for t in self.match(L, v[1]):
                    t.TargetAnim = v[2]
                st["pc"] = pc + 3; continue
            if op in (0x5A, 0x5B):
                dparam = v[2] if op == 0x5A else v[3]
                for t in self.match(L, v[1]):
                    if op == 0x5B:
                        t.TargetAnim = v[2]
                    if dparam >> 5 == 0:
                        t.TargetDir = dparam & 0x1F
                st["pc"] = pc + sz; continue
            if op == 0x27:
                h = self.hero
                dx, dy = h.PosX - L.PosX, h.PosY - L.PosY
                if abs(dx) * 2 > abs(dy) * 3:
                    L.TargetDir = 24 if dx > 0 else 8
                else:
                    L.TargetDir = 0 if dy > 0 else 16
                st["pc"] = pc + 1; continue
            if op == 0x0B:
                L.TargetAnim = v[1]
                if st.get("bpc") != pc:
                    st["bpc"] = pc
                    st["bx"], st["by"] = L.PosX, L.PosY
                    self.events.append((self.tick, L.name, "0x0B @%d first r=%d" % (pc, v[2] | v[3] << 8), L.px()))
                    return
                r = v[2] | v[3] << 8
                dx = abs(st["bx"] - L.PosX) >> 16
                dy = abs(st["by"] - L.PosY) >> 16
                if L.FA and not (dx >= r or dy >= r) and st.get("fa_logged") != pc:
                    st["fa_logged"] = pc
                    self.events.append((self.tick, L.name, "0x0B @%d sees FA=1 (contact %s) -> DLL detour would engage" % (pc, L.last_contact), L.px()))
                if dx >= r or dy >= r:
                    st["bpc"] = None
                    st["fa_logged"] = None
                    self.events.append((self.tick, L.name, "0x0B @%d ends" % pc, L.px()))
                    st["pc"] = pc + 4; continue
                return
            if op == 0x24:
                if L.FA:
                    self.events.append((self.tick, L.name, "0x24 @%d ends (contact %s)" % (pc, L.last_contact), L.px()))
                    st["pc"] = pc + 1; continue
                if st.get("fpc") != pc:
                    st["fpc"] = pc
                    L.last_contact = None
                    self.events.append((self.tick, L.name, "0x24 @%d first" % pc, L.px()))
                return
            if op == 0x1C:
                if st.get("rpc") != pc:
                    st["rpc"] = pc
                    st["rc"] = 0
                    L.complete = 0
                    return
                if L.hold:
                    L.CurAnim = None  # restart next UpdateAnimation
                    L.hold = 0
                    st["rc"] += 1
                    L.complete = 0
                elif L.complete:
                    st["rc"] += 1
                    L.complete = 0
                if st["rc"] >= v[1]:
                    st["rpc"] = None
                    st["pc"] = pc + 2; continue
                return
            if op == 0x19:
                L.status = 3
                self.events.append((self.tick, L.name, "deactivate", L.px()))
                st["pc"] = pc + 1; continue
            if op in (0x62, 0x63):
                mask = v[2] | v[3] << 8
                for t in self.match(L, v[1]):
                    if op == 0x62:
                        t.Flags |= mask
                    else:
                        t.Flags &= ~mask
                st["pc"] = pc + 4; continue
            if op == 0x2D:
                r = self.recs[v[1]]
                if self.find(v[1]) is None:
                    x = int(r["XPos"]) * 12 + 12
                    y = int(r["YPos"]) * 8 + 8
                    z = int(r["Height"]) * 8
                    self.spawn(v[1], x, y, z)
                    self.events.append((self.tick, e.name, "activate rec%d" % v[1], (x, y)))
                st["pc"] = pc + 2; continue
            if op == 0x2E:
                for t in self.match(L, v[1]):
                    t.status = 4
                    t.trigger = None
                    self.events.append((self.tick, e.name, "destroy %s" % t.name, t.px()))
                st["pc"] = pc + 2; continue
            if op == 0x8A:
                x, y, z = v[2] | v[3] << 8, v[4] | v[5] << 8, v[6] | v[7] << 8
                self.spawn(v[1], x, y, z)
                st["pc"] = pc + 8; continue
            if op == 0x64:
                x, y, z = v[2] | v[3] << 8, v[4] | v[5] << 8, v[6] | v[7] << 8
                for t in self.match(L, v[1]):
                    t.PosX, t.PosY, t.PosZ = x << 16, y << 16, (z << 16) + 1
                    t.refresh()
                st["pc"] = pc + 8; continue
            if op == 0x3B:
                h = self.hero
                tx, ty = (h.PosX >> 16) // 24, (h.PosY >> 16) // 16
                st["result"] = 1 if (v[1] <= tx <= v[2] and v[3] <= ty <= v[4]) else 0
                st["pc"] = pc + 7; continue
            if op == 0x43:
                st["logic"] = self.find(v[1]) if v[1] != 129 else self.hero
                st["pc"] = pc + 2; continue
            if op == 0x42:
                st["logic"] = self.hero
                st["pc"] = pc + 1; continue
            raise NotImplementedError("op 0x%02X @%d (%s)" % (op, pc, e.name))

    def match(self, L, sel):
        if sel & 0x80 == 0:
            return [t for t in self.ents if t.rec == sel and t.status in (1, 2, 3)]
        if sel & 0x7F == 0:
            return [L]
        if sel & 0x7F == 1:
            return [self.hero]
        raise NotImplementedError("selector %d" % sel)

    # ---------------------------------------------------------------- one tick (0x8003B388)
    def step(self):
        # UpdateDestroyedEntities (0x80038634)
        if self.recycle_destroyed:
            self.ents = [e for e in self.ents if e.status != 4]
        # events (0x800386D0): map program first (logic = hero), then entities in slot order
        triggers = []
        for e in self.ents:
            if e.player:
                continue
            if e.status == 1:
                e.status = 2
                triggers.append((e, "A"))
            elif e.status == 2:
                triggers.append((e, "C"))
            elif e.status == 3:
                triggers.append((e, "E"))
        if self.map_prog is not None:
            self.run_program(self.hero, self.map_prog, self.hero)
        for e, slot in triggers:
            if slot == "A":
                if e.progA:
                    st = {"pc": e.progA}
                    self.run_program(e, st, e)
                if e.progC:
                    e.cstate = {"pc": e.progC}
            elif slot == "C":
                st = getattr(e, "cstate", None)
                if st is not None and st["pc"] is not None:
                    self.run_program(e, st, e)
            elif slot == "E":
                if self.destroy_on_deact:
                    e.status = 4   # 0x8007ED10 -> DestroyEntity
                    self.events.append((self.tick, e.name, "native E destroy", e.px()))
        # lists (0x800384F4)
        self.collidables = [e for e in self.ents if e.collidable()]
        active = [e for e in self.ents if e.status in (2, 3)]
        if self.hero is not None and self.hero not in active:
            active.insert(0, self.hero)
        # animation
        for e in active:
            e.lag_anim = e.CurAnim
            self.update_animation(e)
        # physics (0x80038364)
        for e in active:
            e.FA = 0
            e.refresh()
        for e in active:
            self.forces(e)
        for e in active:
            self.compute_z(e)
            e.ret130 = self.cxy(e)
        self.tick += 1


def hero_hdr():
    return mlib.sprite_records()[HERO_ID]
