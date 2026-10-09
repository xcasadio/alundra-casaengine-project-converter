"""scenarios_h2.py - E19.h2 D1: replay of the vertical state of an NPC in the air (impulse jumps IZF 1360 / 1280 / 768 / 1024 and the TR-V bouquets)
on the REAL code of ALUN_CD.EXE (France), with TWO independent engines and TWO rules models, and write the per-update tables.

Engine A  : zsim.py (mips.py interpreter, first discovery's harness: own entity/cell set-up, RunScript loop re-written in python).
Engine B  : vz.py   (emu.py interpreter, the h1b2 counter-verifier's harness: own set-up through the REAL box initialiser 0x80039C40, the REAL RunScript 0x8004205C).
Model Z   : zmodel.py (first discovery's python rules) ; model Zd: zmodel_dll.py (the same rules in DLL convention, PosZ = binary - 1) ;
Model M   : class M of rules_vs_emu.py (the counter-verifier's own rules, not derived from zmodel.py), loaded from that file by text extraction.

Convention of time (h2.json summary, checked at 0x8003B388): in one tick the binary runs scripts (0x800386D0), the lists (0x800384F4), UpdateAnimation (0x80038E18), then the
physics pass P (0x80038364).  UPDATE k of the entity is the pass of the tick s+k-1 (s = tick of the `1A` / `1B` instruction, update 1 = the pass that applies the impulse
or the force).  The wait `0x25` (0x8003DB7C) reads +0x140 / +0x144 left by the PREVIOUS pass, so a condition that appears at update k makes it return at tick s+k.

UpdateAnimation is NOT executed (limit of zsim): the stand-in `A` is the three lines of run_ticks.py of the hero (cible == courante, +0xF8 = IZF of the animation at a change,
else 0).  It is therefore valid for jumps with ONE animation change (the 2nd jump of Bergus @839, which asks for the animation already current, is out of reach).

Usage:  python -B scenarios_h2.py [outdir]        (default: ../out)      no argument besides the optional output directory
"""
import sys, os, json, io, re
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.stdout.reconfigure(encoding="utf-8")

import zsim as ZS
from zsim import Sim, ent, M32, s32, U, STATE, SENT, SP
import zmodel as Z
import zmodel_dll as ZD
import vz as VZ

if ZS.Map is None:   # zsim swallows the failure of `from m import Map, DATA`: do not let a map case fail later and more obscurely
    raise SystemExit("zsim.Map is None: m.py could not be imported (EventOpcodeSizeTable.cs / AlundraEventProgramRunner.cs missing?)")
SPR = ZS.SPR

# --------------------------------------------------------------------------------------------------------------------------------------------
# the counter-verifier's rules model M (rules_vs_emu.py), extracted as text so that it is used UNCHANGED
# --------------------------------------------------------------------------------------------------------------------------------------------
_src = open(os.path.join(HERE, "rules_vs_emu.py"), encoding="utf-8").read()
_a = _src.index("def s32_(x):")
_b = _src.index("rnd = random.Random(")
_ns = {}
exec(_src[_a:_b], _ns)
RulesM, s32_ = _ns["M"], _ns["s32_"]

REC_ADDR = 0x80420000
POLL_STATE = STATE + 0x100 * 60          # scratch program state for the 0x25 poll (engine A)


def fl_of(h):
    return h["MoreFlags"] | (h["CanPickup"] << 8) | (h["FlagsPortraitShadowType"] << 16)


def izf_table(h):
    """anim -> signed IZF halfword, as UpdateAnimation reads it (lh 0xA(animset))"""
    return {a["Anim"]: ((a["IsZForceApplied"] + 0x8000) & 0xFFFF) - 0x8000 for a in h["AnimSets"]}


def record_xy(r):
    return (int(r["XPos"]) * 12 + 12) << 16, (int(r["YPos"]) * 8 + 8) << 16


# --------------------------------------------------------------------------------------------------------------------------------------------
# a case
# --------------------------------------------------------------------------------------------------------------------------------------------
class Case:
    def __init__(self, name, title, flags, off, size, animtab, program, *, map_id=None, rec=None, hb=0, G=128, V=4096, rest_up_px=0,
                 events=None, nupd=26, src="", ev_note="", tail_1b=False):
        self.name, self.title, self.flags, self.off, self.size = name, title, flags, off, size
        self.animtab, self.program = animtab, program      # program: bytes, runs on the entity from the tick s (index 0)
        self.map_id, self.rec, self.hb, self.G, self.V = map_id, rec, hb, G, V
        if map_id is not None:
            cp = ZS.Map(map_id).tm.get("custom_properties", {})
            self.G, self.V = int(cp.get("Gravity", G)), int(cp.get("ZViscosity", V))      # header words +4 / +6 of the map (0x80036904 / 0x80036B1C)
        self.rest_up_px = rest_up_px                       # start this many px above the rest height (TR-V: 8)
        self.events = events or {}                         # for the rules models: {update k: ('izf', v) | ('1B', lo, hi) | ('grav', on)}
        self.nupd, self.src, self.ev_note = nupd, src, ev_note
        self.tail_1b = tail_1b                             # TR-V programs: `1B [0,0]` runs in the call in which the 0x25 returns (before update K+1)


REST_TICKS = 3        # physics passes before the program starts (the DLL tests use "une mise a jour de calage")


# --------------------------------------------------------------------------------------------------------------------------------------------
# engine A (zsim)
# --------------------------------------------------------------------------------------------------------------------------------------------
class EngA:
    name = "A (zsim, mips.py)"

    def __init__(self, c):
        self.c = c
        if c.map_id is None:
            self.s = Sim(gravity=c.G, visc=c.V, cells=lambda col, row: (c.hb, 0, 0, 0))
            x, y = 100 << 16, 100 << 16
            z = c.hb * 16 * U + 1 + c.rest_up_px * U
        else:
            self.s = Sim(map_id=c.map_id)
            r = [r for r in self.s.mp.records if int(r["Index"]) == c.rec][0]
            x, y = record_xy(r)
            z = (int(r["Height"]) << 19) - (c.off[2] << 16) + 1 + c.rest_up_px * U
        self.pos0 = (x, y)
        self.idx = 1
        self.s.add(1, c.flags, off=c.off, size=c.size, pos=(x, y, z))
        self.s.init_state(1)
        self.s.put(1, "izf", 0)
        self.has_prog = False

    def load(self, code):
        self.s.load_program(self.idx, code)
        self.has_prog = True

    def rd(self, n):
        return self.s.get(self.idx, n)

    def wr(self, n, v):
        self.s.put(self.idx, n, v)

    def standin(self):
        """UpdateAnimation stand-in (run_ticks.py anim_A: Current = Target, +0xF8 = IZF of the animation at a change, else 0)."""
        s, i = self.s, self.idx
        tgt, cur = s.get(i, "anim_target"), s.get(i, "anim_cur")
        izf = 0
        if tgt != cur:
            s.put(i, "anim_cur", tgt)
            izf = self.c.animtab.get(tgt, 0)
        s.put(i, "izf", izf)

    def tick(self, run_script=True):
        s, i = self.s, self.idx
        tr = s.run_script(i) if (run_script and self.has_prog) else []
        s.rebuild_lists()
        self.standin()
        s.physics()
        s.tick_no += 1
        return tr

    def state(self):
        s, i = self.s, self.idx
        return dict(posz=s.get(i, "posz"), forcez=s.get(i, "forcez"), finalz=s.get(i, "finalz"), coll=s.get(i, "coll"), ground=s.get(i, "ground"),
                    terrain=s.get(i, "terrain"), floor=s.get(i, "floor"), posx=s.get(i, "posx"), posy=s.get(i, "posy"), flags=s.get(i, "flags") & M32)

    def poll25(self):
        s, i = self.s, self.idx
        m = s.mem
        h = m.rw(0x80098FAC + 4 * 0x25)
        cpu = ZS.Cpu(m, {})
        cpu.r[4], cpu.r[5], cpu.r[6], cpu.r[7] = m.rw(ent(i) + ZS.F["logic"]), ent(i), POLL_STATE + 4, POLL_STATE
        cpu.r[29] = SP
        cpu.run(h, SENT, max_steps=10000)
        return s32(cpu.r[2])

    def pc(self):
        pr = self.s.progs[self.idx]
        return self.s.mem.rw(pr["st"] + 4) - pr["base"]


# --------------------------------------------------------------------------------------------------------------------------------------------
# engine B (vz, emu.py, REAL RunScript 0x8004205C)
# --------------------------------------------------------------------------------------------------------------------------------------------
class EngB:
    name = "B (vz, emu.py, real RunScript)"

    def __init__(self, c):
        self.c = c
        self.idx = 1
        if c.map_id is None:
            w = VZ.World(c.G, c.V)
            w.flat(c.hb)
            x, y = 100 << 16, 100 << 16
            z = c.hb * 16 * U + 1 + c.rest_up_px * U
            height = 0
        else:
            mp = ZS.Map(c.map_id)
            w = VZ.World(int(mp.tm["custom_properties"]["Gravity"]), int(mp.tm["custom_properties"]["ZViscosity"]))
            w.load_cells(mp.cells, mp.W, mp.H)
            r = [r for r in mp.records if int(r["Index"]) == c.rec][0]
            x, y = record_xy(r)
            height = int(r["Height"])
            z = (height << 19) - (c.off[2] << 16) + 1 + c.rest_up_px * U
        self.w = w
        w.add(1, c.flags, (x, y, z), size=c.size, off=c.off, height=height)
        self.pos0 = (x, y)
        T = w.probe(1)                                 # InitializeEntity: PosZ <= T+1 -> T+1
        if w.rd(1, "posz") <= T + 1:
            w.wr(1, "posz", T + 1)
        w.wr(1, "terr", T)
        w.m.call(0x80038064, VZ.eaddr(1))              # end pass (FloorHeight, IsOnGround)
        self.has_prog = False

    def load(self, code):
        w, e = self.w, VZ.eaddr(1)
        w.load_code(list(code), {k: [] for k in "ABCDEF"})
        w.m.ww(e + VZ.F["st"], VZ.CODE)
        w.m.ww(e + VZ.F["st"] + 4, VZ.CODE)
        w.m.ww(e + VZ.F["kindmark"], 2)
        self.has_prog = True

    def rd(self, n):
        w = self.w
        off = {"anim_target": 0x88, "anim_cur": 0x90, "izf": 0xF8}.get(n)
        if off is not None:
            return s32(w.m.rw(VZ.eaddr(1) + off))
        return w.rd(1, {"forcez": "forceZ", "posz": "posz", "finalz": "ffz", "coll": "contact", "ground": "onground", "terrain": "terr", "floor": "floor",
                        "posx": "posx", "posy": "posy"}[n])

    def wr(self, n, v):
        off = {"anim_target": 0x88, "anim_cur": 0x90, "izf": 0xF8}[n]
        self.w.m.ww(VZ.eaddr(1) + off, v & M32)

    def standin(self):
        """the same stand-in as engine A (shared assumption)"""
        tgt, cur = self.rd("anim_target"), self.rd("anim_cur")
        izf = 0
        if tgt != cur:
            self.wr("anim_cur", tgt)
            izf = self.c.animtab.get(tgt, 0)
        self.wr("izf", izf)

    def tick(self, run_script=True):
        w = self.w
        if run_script and self.has_prog:
            w.runscript(1, 2)
        w.build_lists()
        self.standin()
        w.physics()
        return []

    def state(self):
        w = self.w
        e = VZ.eaddr(1)
        return dict(posz=w.rd(1, "posz"), forcez=w.rd(1, "forceZ"), finalz=w.rd(1, "ffz"), coll=w.rd(1, "contact"), ground=w.rd(1, "onground"),
                    terrain=w.rd(1, "terr"), floor=w.rd(1, "floor"), posx=w.rd(1, "posx"), posy=w.rd(1, "posy"), flags=w.m.rw(e + VZ.F["flags"]))

    def poll25(self):
        w = self.w
        e = VZ.eaddr(1)
        h = w.m.rw(0x80098FAC + 4 * 0x25)
        return s32(w.m.call(h, e, e, e + VZ.F["st"] + 4, e + VZ.F["st"]))

    def pc(self):
        return self.w.m.rw(VZ.eaddr(1) + VZ.F["st"] + 4) - VZ.CODE


# --------------------------------------------------------------------------------------------------------------------------------------------
# rules models (Z-only, flat terrain T)
# --------------------------------------------------------------------------------------------------------------------------------------------
def run_models(c, T, posz0_b):
    """returns {'Z': rows, 'Zd': rows, 'M': rows}: rows[k] for the updates k=1..nupd (after the REST_TICKS rest passes), tuples
       (posz_bin, forcez, finalz, coll, ground)."""
    grav = bool(c.flags & 0x100)
    modz = c.off[2] << 16
    depth = (c.size[2] << 16) - 1 if c.size[2] else 0
    b = Z.Ent(posz=posz0_b, forcez=0, gravity=grav, modz=modz, depth=depth)
    d = Z.Ent(posz=posz0_b - 1, forcez=0, gravity=grav, modz=modz, depth=depth)
    m = RulesM(posz0_b, 0, grav, modz, depth, T, c.G, c.V)
    out = {"Z": [], "Zd": [], "M": []}
    for _ in range(REST_TICKS):
        Z.physics(b, T, c.G, c.V); ZD.physics_dll(d, T, c.G, c.V); m.tick(0)
    for k in range(1, c.nupd + 1):
        imp = 0
        for ev in c.events.get(k, []):
            if ev[0] == "izf":
                b.izf = ev[1]; d.izf = ev[1]; imp = ev[1]
            elif ev[0] == "1B":
                Z.op1B(b, ev[1], ev[2]); Z.op1B(d, ev[1], ev[2]); m.forceZ = b.forcez
            elif ev[0] == "grav":
                b.gravity = d.gravity = m.grav = bool(ev[1])
        Z.physics(b, T, c.G, c.V); ZD.physics_dll(d, T, c.G, c.V); m.tick(s32_(imp) if imp else 0)
        out["Z"].append((b.posz, b.forcez, b.finalz, b.coll, b.ground))
        out["Zd"].append((d.posz + 1, d.forcez, d.finalz, d.coll, d.ground))      # +1 to compare in the binary convention
        out["M"].append((m.posz, m.forceZ, m.ffz, m.contact, m.onground))
    return out


# --------------------------------------------------------------------------------------------------------------------------------------------
# run a case on one engine
# --------------------------------------------------------------------------------------------------------------------------------------------
def run_engine(Eng, c):
    e = Eng(c)
    start = e.state()
    for _ in range(REST_TICKS):
        e.tick(run_script=False)
    rest = e.state()
    posz_rest = rest["posz"]
    e.load(c.program)
    rows = []
    pcs = []
    xy_moved = False
    for k in range(1, c.nupd + 1):
        e.tick(run_script=True)               # tick s+k-1
        st = e.state()
        st["k"] = k
        st["tick"] = k - 1                    # tick offset from s
        st["poll25_next"] = e.poll25()        # what the wait 0x25 reads at the NEXT tick (s+k): the flags of THIS pass
        st["pc"] = e.pc()
        if (st["posx"], st["posy"]) != e.pos0:
            xy_moved = True
        rows.append(st)
    return dict(engine=Eng.name, start=start, rest=rest, rows=rows, xy_moved=xy_moved)


def first_return(rows, endpc):
    """first tick offset (from s) at which the real script reached `endpc` (the instruction after the 0x25 wait): the tick at which 0x25 returned 1."""
    for r in rows:
        if r["pc"] >= endpc:
            return r["tick"]
    return None


def first_poll(rows):
    """first tick offset s+K at which a free-standing poll of 0x25 reads 1 (K = update index of the first update with contact or IsOnGround)"""
    for r in rows:
        if r["poll25_next"]:
            return r["k"]
    return None


# --------------------------------------------------------------------------------------------------------------------------------------------
# the cases
# --------------------------------------------------------------------------------------------------------------------------------------------
def prog_jump(anim, wait, tail=True):
    # 1A [anim] ; 37 [wait] ; 25 ; 1A [0] ; 00 (Break) ; FF.   0x25 is at offset 4 with `37` of 2 bytes (1A 2 bytes + 37 2 bytes), the instruction after it at 5.
    return bytes([0x1A, anim, 0x37, wait, 0x25, 0x1A, 0x00, 0x00, 0xFF])


END_AFTER_25 = 5          # pc offset of the `1A [0]` that follows the 0x25 in prog_jump(): reached the tick the 0x25 returns


def cases():
    out = []
    P = (-10, -7, 0), (20, 14, 32)                 # box of the NPC prefabs of the chain (Bergus / Giles / UJ-1 montage)
    GR = 0x180                                     # Gravity | 0x80 as the montages of zsim / the DLL tests
    # --- synthetic montage: flat 0, gravity, anim 3 with the IZF of the family
    for izf, label in ((1360, "S-1360"), (1280, "S-1280"), (768, "S-768"), (1024, "S-1024")):
        out.append(Case(label, "synthetic montage, flat 0, G 128 / V 4096, Gravity, box 20x14x32 (off -10,-7,0), anim 3 = IZF %d" % izf, GR, P[0], P[1],
                        {0: 0, 3: izf}, prog_jump(3, 3), events={1: [("izf", izf)]}, nupd=26,
                        src="UJ-1 / UJ1b montage of docs/plan-e19-d2c-valeurs.md A.1 (AlundraAnimationImpulseTests.cs:45,68)"))
    # --- TR-V: bouquet without gravity, 8 px above the floor, 1B [128,255] ; 25 ; 1B [0,0]
    trv_prog = bytes([0x1B, 0x80, 0xFF, 0x25, 0x1B, 0x00, 0x00, 0x00, 0xFF])
    out.append(Case("S-TRV", "synthetic TR-V: no gravity (flags 0x80), 8 px above flat 0, `1B [128,255]` ; `25` ; `1B [0,0]`", 0x80, (-12, -8, 0), (24, 16, 16),
                    {0: 0}, trv_prog, rest_up_px=8, events={1: [("1B", 0x80, 0xFF)]}, nupd=24, tail_1b=True,
                    src="programme 10 C @4738 `1B [128,255]`, @4741 `0x25`, @4742 `1B [0,0]` (rec69) ; @5542/@5545/@5546 (rec81)"))
    out.append(Case("S-TRV-keep", "synthetic TR-V WITHOUT the `1B [0,0]` tail (the force -32768 is kept after the 0x25): shows the contact that the tail prevents", 0x80, (-12, -8, 0), (24, 16, 16),
                    {0: 0}, bytes([0x1B, 0x80, 0xFF, 0x25, 0xFF]), rest_up_px=8, events={1: [("1B", 0x80, 0xFF)]}, nupd=24,
                    src="variant of the programme above (h2.json: 'contact a la 17' holds only if the force is kept)"))
    # --- real prefabs on real cells (Z only: the entity does not move in XY, asserted)
    def real(label, title, map_id, rec, anim, wait, src, no25=False):
        r = [r for r in ZS.Map(map_id).records if int(r["Index"]) == rec][0]
        h = SPR[r["PrefabAssetId"]]
        tab = izf_table(h)
        flags = fl_of(h)
        off = (h["OffsetX"], h["OffsetY"], h["OffsetZ"])
        size = (h["SizeX"], h["SizeY"], h["SizeZ"])
        izf = tab[anim]
        c = Case(label, title + " (prefab %s, flags %#x, off %s, size %s, anim %d = IZF %d)" % (r["PrefabAssetId"][:8], flags, off, size, anim, izf), flags, off, size,
                 tab, prog_jump(anim, wait), map_id=map_id, rec=rec, events={1: [("izf", izf)]}, nupd=26, src=src)
        out.append(c)
    real("R-1360-Giles10", "Giles, map 10 rec98, at his record position", 10, 98, 3, 3, "programme 10 C[74] @6389 `1A [3]`, @6391 `37 [3]`, @6393 `0x25`")
    real("R-1360-Bergus165", "Bergus, map 165 rec2 (C[3]), at his record position", 165, 2, 2, 2, "programme 165 C[3] @834 `1A [2]`, @836 `37 [2]`, @838 `0x25`")
    real("R-1360-Bergus179", "Bergus, map 179 rec8 (B[2] logic entity 8), at his record position", 179, 8, 2, 3, "programme 179 B[2] @411 `1A [2]`, @413 `37 [3]`, @415 `0x25`")
    real("R-1360-Bergus172", "Bergus, map 172 rec8, at his record position", 172, 8, 2, 2, "programme 172 C[8] @606 `1A [2]`, @608 `37 [2]`, @610 `0x25`")
    real("R-768-Dog478a", "dog, map 478 rec3 (Height 2 = 16 px), at its record position", 478, 3, 12, 3, "programme 478 C[3] @329 `5B [128,12,66]` (turn with anim 12; no 0x25 on the dogs)")
    real("R-768-Dog478b", "dog, map 478 rec4 (Height 2 = 16 px), at its record position", 478, 4, 12, 3, "programme 478 C[4] @345 `5B [128,12,67]`")
    real("R-1280-GilesAnim16", "Giles anim 16 (IZF 1280; NOT written by any program of the chain), map 10 rec98", 10, 98, 16, 3, "prefab AnimSets[16] only (family check of IZF 1280 on a real prefab)")
    # bomb: gravity bit cleared (0x17 @628) at the start, `1A [2]` then `0x16` (gravity back on) in the same call
    r = [r for r in ZS.Map(362).records if int(r["Index"]) == 5][0]
    h = SPR[r["PrefabAssetId"]]
    tab = izf_table(h)
    flags = fl_of(h) & ~0x100
    bprog = bytes([0x1A, 2, 0x16, 0x37, 3, 0x25, 0x00, 0xFF])
    out.append(Case("R-1024-Bomb362", "bomb, map 362 rec5 (Height 2 = 16 px), prefab flags %#x with Gravity cleared by `17` @628, then `1A [2]` + `16` @635/@637 in one call" % fl_of(h),
                    flags, (h["OffsetX"], h["OffsetY"], h["OffsetZ"]), (h["SizeX"], h["SizeY"], h["SizeZ"]), tab, bprog, map_id=362, rec=5,
                    events={1: [("izf", 1024), ("grav", 1)]}, nupd=26, src="programme 362 C[5] @628 `17`, @635 `1A [2]`, @637 `16` (anim 2 = IZF 1024)"))
    # bouquets on their real cells: gravity cleared by `63 [128,255,249]` of A[4] / A[5]
    for rec, label in ((69, "R-TRV-rec69"), (81, "R-TRV-rec81")):
        r = [r for r in ZS.Map(10).records if int(r["Index"]) == rec][0]
        h = SPR[r["PrefabAssetId"]]
        flags = fl_of(h) & ~0xF9FF & M32      # `63 [128,255,249]` of A[4] / A[5] clears the mask 0xF9FF of the low 16 bits (checked with the real handler below)
        out.append(Case(label, "bouquet map 10 rec%d at its RECORD position (the drop is elsewhere), no gravity after `63 [128,255,249]`, 8 px above the floor" % rec,
                        flags, (h["OffsetX"], h["OffsetY"], h["OffsetZ"]), (h["SizeX"], h["SizeY"], h["SizeZ"]), {0: 0}, trv_prog, map_id=10, rec=rec, rest_up_px=8,
                        events={1: [("1B", 0x80, 0xFF)]}, nupd=24, tail_1b=True, src="programme 10 C[%d] @%d" % (44 if rec == 69 else 54, 4738 if rec == 69 else 5542)))
    return out


# --------------------------------------------------------------------------------------------------------------------------------------------
# running everything and writing the tables
# --------------------------------------------------------------------------------------------------------------------------------------------
def fields(r):
    return (r["posz"], r["forcez"], r["finalz"], r["coll"], r["ground"])


def main(outdir):
    os.makedirs(outdir, exist_ok=True)
    allres = {}
    summary = []
    for c in cases():
        ra = run_engine(EngA, c)
        rb = run_engine(EngB, c)
        T = ra["rest"]["terrain"]
        K0 = first_poll(ra["rows"])
        if c.tail_1b and K0:
            c.events[K0 + 1] = [("1B", 0, 0)]      # the `1B [0,0]` that follows the 0x25 runs in the call that returns (tick s+K), i.e. before update K+1: placed from the measured K
        models = run_models(c, T, ra["start"]["posz"])
        # is the rest state of the models the same as the engines'?  (compare the first row only through the whole table below)
        diffs = []
        for i, (a, b) in enumerate(zip(ra["rows"], rb["rows"])):
            if fields(a) != fields(b) or a["terrain"] != b["terrain"] or a["poll25_next"] != b["poll25_next"] or a["pc"] != b["pc"]:
                diffs.append(("A!=B", i + 1, fields(a), fields(b), a["terrain"], b["terrain"], a["poll25_next"], b["poll25_next"], a["pc"], b["pc"]))
            for mn in ("Z", "Zd", "M"):
                if fields(a) != models[mn][i]:
                    diffs.append(("A!=" + mn, i + 1, fields(a), models[mn][i]))
        if c.name.startswith(("S-TRV", "R-TRV")):
            endpc = 4          # after `1B [128,255]` (3 bytes) and `25` (1): `1B [0,0]` at offset 4
        elif c.name == "R-1024-Bomb362":
            endpc = 6          # `1A 2 | 16 | 37 3 | 25 | 00`: the Break after the 0x25 is at offset 6
        else:
            endpc = END_AFTER_25
        ret_a = first_return(ra["rows"], endpc) if endpc is not None else None
        ret_b = first_return(rb["rows"], endpc) if endpc is not None else None
        # the first poll can only start at the tick where the program reaches the 0x25 (37 [n] = n+1 ticks after the 1A); the free-standing poll is the lower bound
        K = first_poll(ra["rows"])
        allres[c.name] = dict(title=c.title, src=c.src, terrain=T, rest=ra["rest"], A=ra["rows"], B=rb["rows"], models={k: v for k, v in models.items()},
                              diffs=diffs, return_A=ret_a, return_B=ret_b, first_poll_update=K, xy_moved=(ra["xy_moved"] or rb["xy_moved"]))
        summary.append((c.name, K, ret_a, ret_b, len(diffs), ra["xy_moved"] or rb["xy_moved"]))
        write_table(outdir, c, ra, rb, models, diffs, ret_a, ret_b, K)
    # summary
    buf = io.StringIO()
    print("E19.h2 D1 - replay summary (binary convention PosZ_bin = DLL + 1 ; tick offsets from s = tick of the 1A / 1B ; update k at tick s+k-1)", file=buf)
    print("name | first update with contact or IsOnGround (-> a free-standing 0x25 reads 1 at tick s+K) | tick offset at which the REAL program passed its 0x25 : engine A, engine B | engine/model disagreements | XY moved?", file=buf)
    for n, K, a, b, nd, xy in summary:
        print("%-16s K=%s  return A=%s B=%s  disagreements=%d  xy_moved=%s" % (n, K, ("s+%s" % a) if a is not None else "-", ("s+%s" % b) if b is not None else "-", nd, xy), file=buf)
    open(os.path.join(outdir, "replay_summary.txt"), "w", encoding="utf-8").write(buf.getvalue())
    print(buf.getvalue())
    json.dump(allres, open(os.path.join(outdir, "replay_all.json"), "w", encoding="utf-8"), indent=0)
    return allres


def write_table(outdir, c, ra, rb, models, diffs, ret_a, ret_b, K):
    f = io.StringIO()
    p = lambda *a: print(*a, file=f)
    T = ra["rest"]["terrain"]
    p("### %s" % c.name)
    p(c.title)
    p("source: %s" % c.src)
    p("terrain (+0x138) %d = %.3f px ; map gravity G %d, V %d ; start PosZ_bin %d ; rest PosZ_bin %d (DLL %d) ; REST_TICKS %d passes without script ; flags %#x" % (
      T, T / U, c.G, c.V, ra["start"]["posz"], ra["rest"]["posz"], ra["rest"]["posz"] - 1, REST_TICKS, c.flags & M32))
    p("engines: %s | %s ; models: Z = zmodel.py, Zd = zmodel_dll.py (+1), M = rules_vs_emu.M ; disagreements: %d" % (ra["engine"], rb["engine"], len(diffs)))
    for d in diffs[:10]:
        p("  DISAGREEMENT", d)
    p("update k = tick s+k-1.  Columns: PosZ_bin = engine A ; PosZ_dll = PosZ_bin-1 ; h = (PosZ_dll - T)/65536 px above the rest ; poll = value read by `0x25` at tick s+k (the flags of this pass)")
    p("k | tick | PosZ_bin | PosZ_dll | h(px) | ForceZ | FinalForceZ | contact(+0x140) | IsOnGround(+0x144) | Terrain(+0x138) | FloorHeight(+0x134) | poll 0x25 at s+k | A==B==Z==Zd==M")
    for i, r in enumerate(ra["rows"]):
        same = (fields(r) == fields(rb["rows"][i]) == models["Z"][i] == models["Zd"][i] == models["M"][i])
        p("%d | s+%d | %d | %d | %.6f | %d | %d | %d | %d | %d | %d | %d | %s" % (r["k"], r["tick"], r["posz"], r["posz"] - 1, (r["posz"] - 1 - T) / U, r["forcez"], r["finalz"], r["coll"], r["ground"],
          r["terrain"], r["floor"], r["poll25_next"], "yes" if same else "NO"))
    p("first update with contact or IsOnGround: K=%s -> a free-standing 0x25 reads 1 at tick s+%s" % (K, K))
    p("the REAL program passed its 0x25 at tick offset: engine A s+%s, engine B s+%s (pc after each call: A %s ; B %s)" % (ret_a, ret_b,
      [r["pc"] for r in ra["rows"]][:30], [r["pc"] for r in rb["rows"]][:30]))
    first_ground = next((r["k"] for r in ra["rows"] if r["ground"]), None)
    first_coll = next((r["k"] for r in ra["rows"] if r["coll"] and r["k"] > 1 and not (ra["rows"][r["k"] - 2]["coll"])), None)
    p("first update with IsOnGround 1 (after k=1): %s ; first update with contact 1: %s" % (next((r["k"] for r in ra["rows"] if r["ground"]), None), next((r["k"] for r in ra["rows"] if r["coll"]), None)))
    open(os.path.join(outdir, "replay_%s.txt" % c.name), "w", encoding="utf-8").write(f.getvalue())


if __name__ == "__main__":
    main(sys.argv[1] if len(sys.argv) > 1 else os.path.join(HERE, "..", "out"))
