"""zsim.py - [emulation] REAL code of ALUN_CD.EXE (France) for the vertical physics of record entities (no controller notion in the binary).

Runs, on a synthetic memory image built from the exported maps and prefabs:
  * the physics pass P = 0x80038364 (pre-pass, force pass 0x80036828, rider copy, MoveEntity 0x80037E34 = ComputeZ 0x800375E0 then
    ComputeXY 0x80037730, end pass 0x80038064 = FloorHeight 0x80037F28 + IsOnGround),
  * the collidable / active lists builder 0x800384F4 (real),
  * the event handlers of the Z family run for real: 0x16 0x17 0x1B 0x20 0x21 0x22 0x23 0x63 (and 0x25/0x26 for the flags),
  * the script loop of RunScript 0x8004205C is re-implemented in python (14 lines, read at 0x80042280-0x80042344: Break = Parameters[1]=0,
    pc+1, end of the call; handler 0 = end of the call; non-zero = Parameters[1]=0, pc += result, continue; 0xFF = end),
  * 0x64 (set position) re-implemented from its disassembly (0x8003F610: PosX/PosY = word << 16, PosZ = (word << 16) + 1).
Tick order (0x8003B388): events 0x800386D0 (scripts) -> lists 0x800384F4 -> UpdateAnimation 0x80038E18 -> P 0x80038364.
"""
import sys, os, json
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from mips import Mem, Cpu, M32, s32

A = HERE   # h2 annex edit: m.py (map loader) is archived next to zsim.py; was the scratchpad folder e19h-disc/A/tools
sys.path.insert(0, A)
try:
    from m import Map, DATA
    SPR = json.load(open(DATA + "/sprite-records.json", encoding="utf-8"))
except Exception:   # model-only use
    Map = DATA = SPR = None

U = 65536
ENT0 = 0x80127D30          # entity slot 0 (hero)
STRIDE = 0x294
SP = 0x801FFE00
SENT = 0xDEAD0000
MAPHDR = 0x80300000
TILES = 0x80310000
PROG = 0x80400000          # program bytes
STATE = 0x80410000         # program state: +4 curPtr, +8 Parameters[1], +0xC Parameters[2], +0x2C Result
LIST = 0x8013D8D4          # search result list (entries 1..n at +4*i)
ROW = 416

# entity field offsets (see run_ticks.py / init.txt)
F = dict(status=0x10, anim_target=0x88, anim_cur=0x90, flags=0x6C, forcez=0xB8, finalz=0xEC, izf=0xF8, animflags=0xB4,
         posx=0x114, posy=0x118, posz=0x11C, mx=0x1D8, my=0x1DC, mz=0x1E0, modx=0x1E4, mody=0x1E8, modz=0x1EC,
         w=0x1F0, h=0x1F4, d=0x1F8, terrain=0x138, floor=0x134, ground=0x144, coll=0x140, adj=0x13C, platform=0x28,
         logic=0x230, record=0x44)


def ent(i):
    return ENT0 + STRIDE * i


class Sim:
    def __init__(self, map_id=None, gravity=128, visc=4096, cells=None):
        """cells: callable (col,row) -> (height_byte, slope_byte, walk, gprop) or None for the exported map `map_id`."""
        self.mem = m = Mem()
        self.n = 0
        self.tick_no = 0
        self.progs = {}                  # entity index -> dict(pc, state)
        m.ww(0x800E4334, MAPHDR)
        m.wh(MAPHDR + 4, gravity); m.wh(MAPHDR + 6, visc); m.wb(MAPHDR + 8, 0)
        m.ww(0x800DD86C, TILES)
        for x in range(0, 1300):
            m.wh(0x80127168 + 2 * x, min(x // 24, 51))
        if map_id is not None:
            mp = Map(map_id)
            self.mp = mp
            cp = mp.tm.get("custom_properties", {})
            m.wh(MAPHDR + 4, int(cp.get("Gravity", gravity))); m.wh(MAPHDR + 6, int(cp.get("ZViscosity", visc)))
            self.gravity, self.visc = int(cp.get("Gravity", gravity)), int(cp.get("ZViscosity", visc))
            W = mp.W
            for row in range(mp.H):
                for col in range(W):
                    i = row * W + col
                    a = TILES + 0x604 + row * ROW + col * 8
                    m.wh(a, mp.cells["walkability"][i] | (mp.cells["ground_property"][i] << 8))
                    m.wb(a + 2, mp.cells["slope"][i]); m.wb(a + 3, mp.cells["height"][i])
        else:
            self.mp = None
            self.gravity, self.visc = gravity, visc
            for row in range(60):
                for col in range(52):
                    a = TILES + 0x604 + row * ROW + col * 8
                    hb, sl, wk, gp = cells(col, row) if cells else (0, 0, 0, 0)
                    m.wh(a, wk | (gp << 8)); m.wb(a + 2, sl); m.wb(a + 3, hb)
        m.ww(0x80127000, 0); m.ww(0x80127164, 0)

    # ---- entities -------------------------------------------------------------------------------------------------
    def add(self, idx, flags, off=(0, 0, 0), size=(0, 0, 0), pos=(0, 0, 0), status=2, rest=False, izf=0, animflags=0):
        """pos in 16.16 binary units (PosZ raw as the binary stores it). idx>=1 (slot 0 is the hero, left empty)."""
        m, P = self.mem, ent(idx)
        ox, oy, oz = off; sx, sy, sz = size
        m.ww(P + F["status"], status)
        m.ww(P + F["flags"], flags)
        m.ww(P + 0x108, (-(ox << 16)) & M32); m.ww(P + 0x10C, (-(oy << 16)) & M32)
        m.ww(P + F["modx"], (ox << 16) & M32); m.ww(P + F["mody"], (oy << 16) & M32); m.ww(P + F["modz"], (oz << 16) & M32)
        m.ww(P + 0xFC, (0x4E00000 - ((ox + sx) << 16)) & M32); m.ww(P + 0x100, (0x3C00000 - ((oy + sy) << 16)) & M32)
        m.ww(P + 0x104, (0x7800000 - ((oz + sz) << 16)) & M32)
        m.ww(P + F["w"], ((sx << 16) - 1) & M32 if sx else 0)
        m.ww(P + F["h"], ((sy << 16) - 1) & M32 if sy else 0)
        m.ww(P + F["d"], ((sz << 16) - 1) & M32 if sz else 0)
        m.ww(P + F["posx"], pos[0] & M32); m.ww(P + F["posy"], pos[1] & M32); m.ww(P + F["posz"], pos[2] & M32)
        m.ww(P + F["mx"], (pos[0] + (ox << 16)) & M32); m.ww(P + F["my"], (pos[1] + (oy << 16)) & M32); m.ww(P + F["mz"], (pos[2] + (oz << 16)) & M32)
        m.ww(P + F["izf"], izf & M32); m.ww(P + F["animflags"], animflags)
        m.ww(P + F["logic"], P)          # logic entity = itself (InitializeEntity 0x8004201C)
        m.ww(P + 0x1D4, 0)
        self.n = max(self.n, idx + 1)
        self.rebuild_lists()
        # InitializeEntity ground state (0x80039EE0..): T, clamp, then 0x80038064 (FloorHeight, IsOnGround)
        return P

    def init_state(self, idx, clamp=True):
        """what InitializeEntity (0x80039D04) does after placing the entity: T = probe, PosZ <= T+1 -> PosZ = T+1, end pass."""
        m, P = self.mem, ent(idx)
        T = self.probe(P)
        m.ww(P + F["terrain"], T & M32)
        if clamp and s32(m.rw(P + F["posz"])) <= T + 1:
            m.ww(P + F["posz"], (T + 1) & M32)
            m.ww(P + F["mz"], (T + 1 + s32(m.rw(P + F["modz"]))) & M32)
        self.end_pass(P)

    def rebuild_lists(self):
        m = self.mem
        m.ww(0x80134360, self.n - 1)
        c = Cpu(m, {}); c.r[29] = SP
        c.run(0x800384F4, SENT, max_steps=200000)

    def get(self, idx, name):
        return s32(self.mem.rw(ent(idx) + F[name]))

    def put(self, idx, name, v):
        self.mem.ww(ent(idx) + F[name], v & M32)

    def probe(self, P):
        c = Cpu(self.mem, {}); c.r[4] = P; c.r[29] = SP
        c.run(0x800370C4, SENT)
        return s32(c.r[2])

    def end_pass(self, P):
        c = Cpu(self.mem, {}); c.r[4] = P; c.r[29] = SP
        c.run(0x80038064, SENT, max_steps=200000)

    # ---- one physics pass ------------------------------------------------------------------------------------------
    def physics(self):
        m = self.mem
        m.ww(SP + 0x1C, SENT)
        cpu = Cpu(m, {0x80037364: 0})
        cpu.r[29] = SP
        cpu.run(0x80038364, SENT, max_steps=400000)

    # ---- scripts ----------------------------------------------------------------------------------------------------
    def load_program(self, idx, code, pc=0):
        """code: bytes of the program; the entity runs it (owner = logic = itself)."""
        m = self.mem
        base = PROG + 0x1000 * idx
        for i, b in enumerate(code):
            m.wb(base + i, b)
        st = STATE + 0x100 * idx
        m.ww(st + 4, base + pc); m.ww(st + 8, 0); m.ww(st + 0xC, 0); m.ww(st + 0x2C, 0)
        self.progs[idx] = dict(base=base, st=st)

    def run_script(self, idx, max_ops=200):
        """RunScript 0x8004205C main loop (0x80042280-0x80042344) around the REAL handlers.  Returns list of (pc_offset, opcode, result)."""
        m = self.mem
        pr = self.progs[idx]
        st, base = pr["st"], pr["base"]
        owner = ent(idx)
        trace = []
        stubs = {0x8003C954: self._search, 0x8003C840: 0}
        for _ in range(max_ops):
            ptr = m.rw(st + 4)
            op = m.rb(ptr)
            logic = m.rw(owner + F["logic"])
            if op == 0xFF:
                trace.append((ptr - base, op, None)); return trace
            if op == 0:
                m.ww(st + 8, 0); m.ww(st + 4, ptr + 1)
                trace.append((ptr - base, 0, 0)); return trace
            if op == 0x64:
                r = self._op64(logic, ptr)
            else:
                h = m.rw(0x80098FAC + 4 * op)
                cpu = Cpu(m, stubs)
                cpu.r[4], cpu.r[5], cpu.r[6], cpu.r[7] = logic, owner, st + 4, st
                cpu.r[29] = SP
                cpu.run(h, SENT, max_steps=100000)
                r = s32(cpu.r[2])
            trace.append((ptr - base, op, r))
            if r == 0:
                return trace
            m.ww(st + 8, 0); m.ww(st + 4, ptr + r)
        raise RuntimeError("script runs away")

    def _search(self, cpu):
        # 0x8003C954(a0 = logic, a1 = type): only the logic-entity type 0x80 is supported
        t = cpu.r[5] & 0xFF
        assert t == 0x80, "search type 0x%x not modelled" % t
        self.mem.ww(LIST + 4, cpu.r[4])
        return 1

    def _op64(self, logic, ptr):
        m = self.mem
        b = [m.rb(ptr + i) for i in range(8)]
        x = (b[2] | (b[3] << 8)) << 16; y = (b[4] | (b[5] << 8)) << 16; z = ((b[6] | (b[7] << 8)) << 16) + 1
        assert b[1] == 0x80
        m.ww(logic + F["posx"], x & M32); m.ww(logic + F["posy"], y & M32); m.ww(logic + F["posz"], z & M32)
        return 8

    # ---- one game tick ----------------------------------------------------------------------------------------------
    def tick(self, scripts=(), rebuild=True):
        """scripts: entity indexes whose program runs in this tick (event pass 0x800386D0 runs a program only while it is loaded)."""
        tr = {}
        for i in scripts:
            tr[i] = self.run_script(i)
        if rebuild:
            self.rebuild_lists()
        # UpdateAnimation 0x80038E18: not run (no animation change in these scenarios); IZF word stays as written by the caller
        self.physics()
        self.tick_no += 1
        return tr

    def snap(self, idx):
        g = lambda n: self.get(idx, n)
        return dict(posz=g("posz"), forcez=g("forcez"), finalz=g("finalz"), terrain=g("terrain"), floor=g("floor"), ground=g("ground"),
                    coll=g("coll"), flags=g("flags") & M32)
