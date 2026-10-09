"""giles_ledge_h2.py - E19.h2 D1 steps 3 and 4: the XY + Z state of an NPC in the air on REAL cells.

  G : Giles (map 10 rec98, prefab 9b582582, flags 0x83a180, box 20x14x32 off -10,-7,0), the jump of C[74] @6389 `1A [3]` (animation 3: speed 128, acceleration byte 1,
      IZF 1360) from the ground (terrain 0) towards the 16 px ledge whose first column is column 22 (x >= 528 px) of the map 10, rows 51-55 (y 816-895): the real force pipeline
      (ComputeTargetForce 0x80036614 + IncrementForce 0x800367E4, run by P) with the animation table of the prefab in memory, the real terrain probe 0x800370C4 on the real cells.
  L : the ledge fall: a gravity NPC (box 20 px wide, off -10) at rest on 16 px cells that end at x = 144 px (column 6 starts at 144), walking east at exactly 1 px per tick from
      PosX 150 px; the first tick in the air (binary: the terrain is probed BEFORE the XY step; DLL today: AFTER the step).

Engines: A = zsim (mips.py), B = vz (emu.py).  Same stand-in for UpdateAnimation as run_ticks.py `anim_A` (Current = Target, +0x9C = animset pointer, +0xF8 = IZF, +0xB4 = byte 0xD),
which is the reading of 0x80038B18-0x80038B70 (checked in the disassembly: +0x9C at 0x80038B34, +0x90 at 0x80038B4C, +0xF8 at 0x80038B64, +0xB4 at 0x80038B70).

NOT measured here and flagged where it matters: the walk BEFORE the jump (the pre-state of the XY force is injected: Giles walks with animation 9, speed 208, which is what the plan derives
for the DLL: step 159744 = 208 * 768), the exact x at the tick of the `1A` (taken from the plan's DLL-derived value 32329728 and swept), every other entity of the scene.

Usage:  python -B giles_ledge_h2.py [outdir]    (default ../out)
"""
import sys, os, json, io
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.stdout.reconfigure(encoding="utf-8")

import zsim as ZS
from zsim import Sim, ent, M32, s32, U
import vz as VZ
import scenarios_h2 as S

ANIMTAB_A = 0x80320000
ANIMTAB_B = 0x80170000
OXT, OYT = 0x80023654, 0x80023694        # direction tables read by 0x80036614 (lh, signed)


def write_animtab(mem, base, animsets):
    for a in animsets:
        e = base + 14 * a["Anim"]
        mem.wh(e + 8, a["Speed"] & 0xFFFF)
        mem.wh(e + 0xA, a["IsZForceApplied"] & 0xFFFF)
        mem.wb(e + 0xC, a["Sfx"])
        mem.wb(e + 0xD, a["Acceleration"] & 0xFF)       # the JSON "Acceleration" is the binary byte 0xD (run_ticks.py, 0x80038B68)


def sh(v):
    return v - 0x10000 if v & 0x8000 else v


class Rig:
    """one NPC on one engine, with the animation table in memory and the full UpdateAnimation stand-in"""

    def __init__(self, which, *, cells_from_map=None, cells_fn=None, flags, off, size, pos, animsets, G=128, V=4096, ):
        self.which = which
        self.animsets = animsets
        if which == "A":
            self.sim = Sim(map_id=cells_from_map) if cells_from_map is not None else Sim(gravity=G, visc=V, cells=cells_fn)
            self.mem = self.sim.mem
            self.base = ANIMTAB_A
            self.P = ent(1)
            self.sim.add(1, flags, off=off, size=size, pos=pos)
            self.sim.init_state(1)
            self.sim.put(1, "izf", 0)
        else:
            if cells_from_map is not None:
                mp = ZS.Map(cells_from_map)
                w = VZ.World(int(mp.tm["custom_properties"]["Gravity"]), int(mp.tm["custom_properties"]["ZViscosity"]))
                w.load_cells(mp.cells, mp.W, mp.H)
            else:
                w = VZ.World(G, V)
                cells = {"walkability": [], "ground_property": [], "slope": [], "height": []}
                for row in range(60):
                    for col in range(52):
                        hb, sl, wk, gp = cells_fn(col, row)
                        cells["walkability"].append(wk); cells["ground_property"].append(gp); cells["slope"].append(sl); cells["height"].append(hb)
                w.load_cells(cells, 52, 60)
            self.w = w
            self.mem = w.m
            self.base = ANIMTAB_B
            self.P = VZ.eaddr(1)
            w.add(1, flags, pos, size=size, off=off)
            T = w.probe(1)
            if w.rd(1, "posz") <= T + 1:
                w.wr(1, "posz", T + 1)
            w.wr(1, "terr", T)
            w.m.call(0x80038064, self.P)
        write_animtab(self.mem, self.base, animsets)

    # ---- state injection (the walk before the 1A): animation `a0` current, direction d, forces steady at the target of that animation
    def prime_walk(self, a0, d, force=None):
        m, P = self.mem, self.P
        sp = [a for a in self.animsets if a["Anim"] == a0][0]
        m.ww(P + 0x88, a0); m.ww(P + 0x90, a0); m.ww(P + 0x9C, self.base + 14 * a0)
        m.ww(P + 0x8C, d); m.ww(P + 0x94, d)
        m.ww(P + 0xF4, sp["Speed"]); m.ww(P + 0xF0, sp["Acceleration"] & 0xF)
        tx, ty = sh(m.rh(OXT + 2 * d)) * sp["Speed"], sh(m.rh(OYT + 2 * d)) * sp["Speed"]
        if force is not None:
            tx, ty = force
        for off, v in ((0xBC, tx), (0xC0, ty), (0xC4, tx), (0xC8, ty), (0xD4, 0), (0xD8, 0)):
            m.ww(P + off, v & M32)
        return tx, ty

    def set_target(self, anim):
        self.mem.ww(self.P + 0x88, anim)

    def standin(self):
        m, P, base = self.mem, self.P, self.base
        tgt, cur = s32(m.rw(P + 0x88)), s32(m.rw(P + 0x90))
        m.ww(P + 0xF8, 0)
        if tgt != cur:
            e = base + 14 * tgt
            m.ww(P + 0x90, tgt); m.ww(P + 0x9C, e)
            m.ww(P + 0xF8, sh(m.rh(e + 0xA)) & M32)
            m.ww(P + 0xB4, m.rb(e + 0xD))

    def tick(self):
        if self.which == "A":
            self.sim.rebuild_lists()
            self.standin()
            self.sim.physics()
        else:
            self.w.build_lists()
            self.standin()
            self.w.physics()

    def rd(self, off):
        return s32(self.mem.rw(self.P + off))

    def poll25(self):
        m, P = self.mem, self.P
        h = m.rw(0x80098FAC + 4 * 0x25)
        return 1 if (self.rd(0x140) or self.rd(0x144)) else 0     # same two words the handler 0x8003DB7C reads (0x140 then 0x144); handler checked in scenarios_h2.py

    def row(self, k):
        return dict(k=k, posx=self.rd(0x114), posy=self.rd(0x118), posz=self.rd(0x11C), forcex=self.rd(0xC4), finalx=self.rd(0xE4), forcez=self.rd(0xB8), finalz=self.rd(0xEC),
                    coll=self.rd(0x140), ground=self.rd(0x144), terrain=self.rd(0x138), floor=self.rd(0x134), izf=self.rd(0xF8), anim=self.rd(0x90), poll=self.poll25())


# --------------------------------------------------------------------------------------------------------------------------------------------
# G : Giles
# --------------------------------------------------------------------------------------------------------------------------------------------
def giles(which, x0, y0=56098816, walk_anim=9, nupd=26, force_override=None):
    r = [r for r in ZS.Map(10).records if int(r["Index"]) == 98][0]
    h = S.SPR[r["PrefabAssetId"]]
    rig = Rig(which, cells_from_map=10, flags=S.fl_of(h), off=(h["OffsetX"], h["OffsetY"], h["OffsetZ"]), size=(h["SizeX"], h["SizeY"], h["SizeZ"]),
              pos=(x0, y0, 1), animsets=h["AnimSets"])
    rig.prime_walk(walk_anim, 24, force_override)
    rig.set_target(walk_anim)
    # rest passes WITHOUT script and WITHOUT motion would move the walker: do not run them (the position at the tick s is x0); the end pass gave the initial flags
    rig.set_target(3)                  # the real handler 0x1A writes exactly this word (+0x88), see scenarios_h2.py (S-1360 etc.)
    rows = []
    for k in range(1, nupd + 1):
        rig.tick()
        rows.append(rig.row(k))
    return rows


def giles_report(rows, T_ledge=1048576, ledge_px=528):
    """K = first update with contact or IsOnGround ; first_terr = first update whose AFTER-PASS +0x138 equals the ledge height"""
    K = next((r["k"] for r in rows if r["ground"] or r["coll"]), None)
    first_terr = next((r["k"] for r in rows if r["terrain"] == T_ledge), None)
    return K, first_terr


# --------------------------------------------------------------------------------------------------------------------------------------------
# L : the ledge
# --------------------------------------------------------------------------------------------------------------------------------------------
def ledge(which, nticks=8, x0_px=150, step_px=1.0, edge_col=6):
    hi = lambda col, row: ((1 if col < edge_col else 0), 4, 0, 0)
    walker = [dict(Anim=0, Speed=1, Acceleration=0, IsZForceApplied=0, Sfx=0)]
    rig = Rig(which, cells_fn=hi, flags=0x180, off=(-10, -7, 0), size=(20, 14, 32), pos=(x0_px << 16, 100 << 16, 1), animsets=walker)
    T = 0
    if which == "A":
        T = rig.sim.get(1, "terrain")
    else:
        T = rig.rd(0x138)
    # rest passes WITHOUT motion first (3 passes, as the Z-only cases), then the walk starts at tick 1
    for _ in range(S.REST_TICKS):
        rig.tick()
    rest = rig.row(0)
    force = int(step_px * U)
    m, P = rig.mem, rig.P
    # injected steady walk: animation 0 (speed 1 in the table), direction 24 (east), caches equal so that 0x80036614 keeps the injected target force
    m.ww(P + 0x88, 0); m.ww(P + 0x90, 0); m.ww(P + 0x9C, rig.base)
    m.ww(P + 0x8C, 24); m.ww(P + 0x94, 24); m.ww(P + 0xF4, 1); m.ww(P + 0xF0, 0)
    for off, v in ((0xBC, force), (0xC0, 0), (0xC4, force), (0xC8, 0), (0xD4, 0), (0xD8, 0)):
        m.ww(P + off, v & M32)
    rows = []
    for t in range(1, nticks + 1):
        rig.tick()
        rows.append(rig.row(t))
    return rest, rows


def hand_ledge(x0_px=150, step_px=1.0, edge_x=144, half_w=10, nticks=8):
    """independent arithmetic (no emulation): the box covers [PosX - 10, PosX + 10) ; it overlaps the 16 px cells (x < edge_x) iff PosX - 10 < edge_x.
       binary: tick t probes the terrain at the position BEFORE the step of tick t (= x0 + (t-1) * step) ; DLL today: AFTER the step (x0 + t * step)."""
    out = []
    for t in range(1, nticks + 1):
        pre, post = x0_px + (t - 1) * step_px, x0_px + t * step_px
        out.append((t, pre, post, pre - half_w < edge_x, post - half_w < edge_x))
    first_bin = next(t for t, pre, post, hb, ha in out if not hb)
    first_dll = next(t for t, pre, post, hb, ha in out if not ha)
    return out, first_bin, first_dll


def main(outdir):
    os.makedirs(outdir, exist_ok=True)
    buf = io.StringIO()
    p = lambda *a: print(*a, file=buf)
    res = {}

    # ---- preliminary checks with the real handler 0x63 (flags of the bouquets) --------------------------------------------------------------
    for rec in (69, 81):
        r = [r for r in ZS.Map(10).records if int(r["Index"]) == rec][0]
        h = S.SPR[r["PrefabAssetId"]]
        fl = S.fl_of(h)
        sim = Sim(gravity=128, visc=4096, cells=lambda c, rr: (0, 4, 0, 0))
        sim.add(1, fl, off=(0, 0, 0), size=(0, 0, 0), pos=(100 << 16, 100 << 16, 1))
        sim.load_program(1, bytes([0x63, 128, 255, 249, 0xFF]))
        tr = sim.run_script(1)
        p("check 0x63 [128,255,249] on the flags of rec%d: %#x -> %#x (real handler 0x%08x, trace %s)" % (rec, fl, sim.get(1, "flags") & M32, sim.mem.rw(0x80098FAC + 4 * 0x63), tr))
        res["check63_rec%d" % rec] = (fl, sim.get(1, "flags") & M32)
    p()

    # ---- G : Giles ------------------------------------------------------------------------------------------------------------------------
    x0 = 32329728          # plan docs/plan-e19-d2c-valeurs.md:28 : "rend a F0+101 en x = 32329728" (DLL-derived, not measured on the binary)
    rowsA = giles("A", x0)
    rowsB = giles("B", x0)
    same = all(tuple(a[k] for k in ("posx", "posy", "posz", "forcex", "forcez", "finalz", "coll", "ground", "terrain", "poll")) ==
               tuple(b[k] for k in ("posx", "posy", "posz", "forcex", "forcez", "finalz", "coll", "ground", "terrain", "poll")) for a, b in zip(rowsA, rowsB))
    p("### G - Giles (map 10 rec98) jumps with animation 3 (IZF 1360, speed 128, accel byte 1) from the ground (terrain 0) onto the 16 px ledge (first column 22 = x >= 528 px)")
    p("start: PosX %d (%.4f px) = plan value at the tick of `1A [3]` @6389 (s = F0+101), PosY %d (%.1f px), rest on terrain 0 ; XY force injected: walking anim 9 (speed 208), direction 24 (east), steady force 159744" % (x0, x0 / U, 56098816, 56098816 / U))
    p("engines A (zsim) and B (vz) identical on posx, posy, posz, forcex, forcez, finalz, contact, ground, terrain, poll: %s" % same)
    p("k | tick | PosX(px) | box right edge (px) | PosZ_bin | PosZ_dll | h above terrain 0 (px) | ForceX | ForceZ | FinalForceZ | contact | IsOnGround | Terrain(+0x138) px, read after the pass (= probe after the XY step) | poll 0x25 at s+k")
    for r in rowsA:
        p("%d | s+%d | %.4f | %.4f | %d | %d | %.4f | %d | %d | %d | %d | %d | %.3f | %d" % (r["k"], r["k"] - 1, r["posx"] / U, r["posx"] / U + 10 - 1 / U, r["posz"], r["posz"] - 1, (r["posz"] - 1) / U, r["forcex"], r["forcez"], r["finalz"],
          r["coll"], r["ground"], r["terrain"] / U, r["poll"]))
    K, first_terr = giles_report(rowsA)
    p("first update with contact or IsOnGround: K=%s -> `0x25 @6393` returns at tick s+%s = F0+%d (s = F0+101: update k is the pass of tick s+k-1, 0x25 reads the pass before)" % (K, K, 101 + K))
    p("Terrain (+0x138) read AFTER a pass is the probe at the position AFTER that pass's XY step (ComputeXY re-probes at 0x80037DC0 ; confirmed by the ledge table L, tick 4: contact 1 from the pre-step probe, Terrain 0 after the step).")
    p("first update after whose XY step the box is over the ledge (Terrain 16 px read after the pass): U=%s ; the binary's ComputeZ (probe BEFORE the step, 0x80037690) uses it from update %s ; a DLL-order step (terrain AFTER the step of the same update) from update %s ; landing update K=%s : %s" % (
      first_terr, (first_terr + 1) if first_terr else None, first_terr, K, ("the order of the probe does not change the landing update (the ledge is known %s update(s) before the landing in the binary, %s in the DLL order)" % (K - first_terr - 1, K - first_terr)) if first_terr and K else "?"))
    res["giles"] = dict(x0=x0, K=K, first_pre_step_terrain=first_terr, rows=rowsA, engines_identical=same)

    # sweep of the start x and of the walking force (sensitivity: what the pre-state of the XY force could change)
    p()
    p("sweep (engine A): x0 (px) at the tick of `1A` and pre-jump steady walk force ; K = first update with contact/IsOnGround ; T_first = first update whose after-pass Terrain (+0x138, probe after the XY step) is the ledge ; xR_K = box right edge (px) after update K-1")
    sweep = []
    xs = [("486", 486 * U), ("488", 488 * U), ("490", 490 * U), ("492", 492 * U), ("493.3 (plan)", x0), ("494", 494 * U), ("496", 496 * U), ("498", 498 * U), ("500", 500 * U),
          ("505", 505 * U), ("510", 510 * U), ("515", 515 * U), ("518", 518 * U), ("520", 520 * U)]
    for walk_anim, fx in ((9, None), (1, None), (0, (0, 0))):
        for lab, xx in xs:
            rws = giles("A", xx, walk_anim=walk_anim, force_override=fx, nupd=40)
            K2 = next((r["k"] for r in rws if r["ground"] or r["coll"]), None)
            ft = next((r["k"] for r in rws if r["terrain"] == 1048576), None)
            xr = (rws[K2 - 2]["posx"] / U + 10) if K2 and K2 >= 2 else None
            sweep.append((walk_anim, fx, lab, K2, ft, xr))
            p("walk anim %s force %s | x0 %12s px | K=%s | T_first=%s | box right edge after update K-1: %s" % (walk_anim, "steady" if fx is None else fx, lab, K2, ft, ("%.3f" % xr) if xr else None))
    res["giles_sweep"] = sweep
    p("fine sweep (engine A, x0 from 490 to 494 px by 1/8 px, 30 updates): the x0 at which K changes (K=23 is a landing on the ground 0 in front of the 16 px wall)")
    fine = {}
    for walk in (9, 1):
        prev = None
        for q in range(0, 4 * 8 + 1):
            xx = int((490 + q / 8.0) * U)
            K3 = next((r["k"] for r in giles("A", xx, walk_anim=walk, nupd=30) if r["ground"] or r["coll"]), None)
            if K3 != prev:
                p("  walk anim %d : x0 = %.3f px -> K=%s" % (walk, xx / U, K3))
                fine.setdefault(walk, []).append((xx / U, K3))
                prev = K3
    res["giles_fine"] = fine
    p("the plan's x0 (493.3125 px) is %.2f px (walk anim 9) / %.2f px (walk anim 1) above the lowest x0 that still lands on the ledge" % (x0 / U - fine[9][-1][0], x0 / U - fine[1][-1][0]))
    p()

    # ---- L : the ledge ---------------------------------------------------------------------------------------------------------------------
    restA, rowsA = ledge("A")
    restB, rowsB = ledge("B")
    hand, first_bin, first_dll = hand_ledge()
    p("### L - ledge fall: gravity NPC (flags 0x180, box 20x14x32, off -10,-7,0), 3 rest passes on 16 px cells (columns 0-5, x < 144 px), then a steady 65536/tick east walk from PosX 150 px (injected force)")
    p("rest: engine A PosZ_bin %d terrain %d ; engine B PosZ_bin %d terrain %d (16 px = 1048576)" % (restA["posz"], restA["terrain"], restB["posz"], restB["terrain"]))
    p("t | PosX(px) A (after the step) | PosZ_dll A | Terrain(+0x138) A, after the pass (= after the step) | contact | IsOnGround | FinalForceZ | in the air? || B: PosX | PosZ_dll | Terrain || hand arithmetic: pre-step x, box left edge, over high cells | post-step x, over high cells")
    first_air_A = first_air_B = None
    for a, b, h in zip(rowsA, rowsB, hand):
        air_a = (a["posz"] - 1) < 1048576
        air_b = (b["posz"] - 1) < 1048576
        if air_a and first_air_A is None: first_air_A = a["k"]
        if air_b and first_air_B is None: first_air_B = b["k"]
        p("%d | %.2f | %d | %d | %d | %d | %d | %s || %.2f | %d | %d || %.1f, %.1f, %s | %.1f, %s" % (a["k"], a["posx"] / U, a["posz"] - 1, a["terrain"], a["coll"], a["ground"], a["finalz"], "AIR" if air_a else "on ground",
          b["posx"] / U, b["posz"] - 1, b["terrain"], h[1], h[1] - 10, h[3], h[2], h[4]))
    p("first tick in the air: engine A tick %s, engine B tick %s ; hand arithmetic (terrain read BEFORE the step): tick %d ; terrain read AFTER the step (DLL order, mechanism a): tick %d" % (first_air_A, first_air_B, first_bin, first_dll))
    res["ledge"] = dict(first_air_A=first_air_A, first_air_B=first_air_B, hand_binary=first_bin, hand_dll_order=first_dll, rowsA=rowsA, rowsB=rowsB, restA=restA, restB=restB)
    open(os.path.join(outdir, "giles_ledge.txt"), "w", encoding="utf-8").write(buf.getvalue())
    json.dump(res, open(os.path.join(outdir, "giles_ledge.json"), "w", encoding="utf-8"), indent=0)
    print(buf.getvalue())


if __name__ == "__main__":
    main(sys.argv[1] if len(sys.argv) > 1 else os.path.join(HERE, "..", "out"))
