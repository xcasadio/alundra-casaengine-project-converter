"""Effect machine: the REAL effect code of ALUN_CD.EXE (France) in the MIPS interpreter, fed with DATAS.BIN sprite-info sections.
Read-only on the binary and the data; everything lives in the interpreter's RAM."""
import sys, os, struct
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import emu as E

DATAS = r"D:\development\repo\Alundra Remake\Alundra (France)\Alundra (France)_extracted\DATA\DATAS.BIN"
_D = open(DATAS, "rb").read()
DIR = struct.unpack_from("<494I", _D, 0)

POOL = 0x80138608
SLOT = 0x80
MAP_DESC = 0x80126E70
GLB_DESC = 0x80126EC0
LIST_PTR = 0x80126E6C       # write cursor of the sprite list
LIST_BASE = 0x8011CB60
LIST_COUNT = 0x8011CE60
BLOCK_MASK = 0x800DC4B8
HERO = 0x80127D30           # +0x120 tile X, +0x124 tile Y
MAPSI = 0x80150000
GLBSI = 0x800E4360
ENT_SEARCH_RESULT = 0x8013D8D8
FAKE_ENT = 0x80180000       # fake entity structs, 0x200 apart


def map_blob(m):
    st, en = DIR[10 + m], DIR[11 + m]
    b = _D[st:en]
    mh = struct.unpack_from("<7I", b, 0)
    return b[mh[3]:mh[4]]


def global_blob():
    return _D[DIR[0]:DIR[1]]


class FxMachine:
    def __init__(self, map_id=None, hero_tile=(0, 0)):
        m = self.m = E.make_machine()
        self.printed = []
        def pr(mm): self.printed.append(("printf", mm.r[4]))
        m.hooks[0x800815FC] = pr
        m.hooks[0x800815EC] = pr
        def bios(mm): raise RuntimeError("BIOS error call 0x800831E0 (effect animation error)")
        m.hooks[0x800831E0] = bios
        # entity search stub (0x8003C954): returns first entity whose key (a1) matches, via self.entities dict key -> entity addr
        self.entities = {}
        def search(mm):
            key = mm.r[5] & 0xFF
            e = self.entities.get(key)
            if e is None:
                mm.r[2] = 0
            else:
                mm.ww(ENT_SEARCH_RESULT, e)
                mm.r[2] = e
        m.hooks[0x8003C954] = search
        # global bank
        g = global_blob()
        for i, c in enumerate(g): m.ram[(GLBSI & 0x1FFFFFFF) + i] = c
        m.call(0x8002D808, GLB_DESC, GLBSI)
        self.map_id = map_id
        if map_id is not None:
            self.load_map_data(map_id)
        m.call(0x8003C17C)                  # slot ids (game init)
        m.ww(LIST_PTR, LIST_BASE); m.ww(LIST_COUNT, 0)
        m.ww(HERO + 0x120, hero_tile[0]); m.ww(HERO + 0x124, hero_tile[1])
        m.ww(BLOCK_MASK, 0)

    def load_map_data(self, map_id):
        m = self.m
        b = map_blob(map_id)
        for i, c in enumerate(b): m.ram[(MAPSI & 0x1FFFFFFF) + i] = c
        m.call(0x8002D808, MAP_DESC, MAPSI)
        self.si = b

    # ---- slots -----------------------------------------------------------------------------------------------
    def slot_addr(self, i): return POOL + SLOT * i

    def slot(self, i):
        m = self.m; a = self.slot_addr(i)
        return dict(
            idx=m.rw(a), rec=m.rw(a + 4), spr=m.rw(a + 8), imgs=m.rw(a + 0x0C), rx=m.rws(a + 0x10), ry=m.rws(a + 0x14), rz=m.rws(a + 0x18),
            rdepth=m.rw(a + 0x1C), bias=m.rws(a + 0x20), n=m.rws(a + 0x24), pagebase=m.rws(a + 0x28), clutbase=m.rws(a + 0x2C),
            recidx=m.rws(a + 0x30), mode=m.rws(a + 0x34), ent=m.rw(a + 0x38), x=m.rws(a + 0x3C), y=m.rws(a + 0x40), z=m.rws(a + 0x44),
            off=(m.rws(a + 0x48), m.rws(a + 0x4C), m.rws(a + 0x50)), forces=(m.rws(a + 0x54), m.rws(a + 0x58), m.rws(a + 0x5C)),
            doff=m.rws(a + 0x60), depth=m.rw(a + 0x64), status=m.rws(a + 0x68),
            tb=m.rb(a + 0x6C), cb=m.rb(a + 0x6D), ts=m.rb(a + 0x6E), cs=m.rb(a + 0x6F), ta=m.rb(a + 0x70), ca=m.rb(a + 0x71),
            fptr=m.rw(a + 0x74), f0=m.rw(a + 0x78), cnt=m.rb(a + 0x7C), dead=m.rb(a + 0x7D))

    def active(self):
        return [i for i in range(128) if self.m.rws(self.slot_addr(i) + 0x68) != 0]

    # ---- engine entry points -------------------------------------------------------------------------------------
    def map_load_spawn(self):               # 0x8003C1A4
        self.m.call(0x8003C1A4)

    def tick(self, blocked=0):              # the effect part of the logic tick: reset list, UpdateEffects
        m = self.m
        m.ww(LIST_PTR, LIST_BASE); m.ww(LIST_COUNT, 0)
        m.ww(BLOCK_MASK, blocked)
        m.call(0x8003C410)
        n = m.rws(LIST_COUNT)
        return [m.rw(LIST_BASE + 4 * k) for k in range(n)]      # slot+0x0C pointers pushed this tick

    def spawn(self, rec, force):            # SpawnMapEffect
        return self.m.call(0x8003C094, rec, force)

    # opcode handlers: (address, operand bytes incl. opcode byte)
    HANDLERS = {0x90: 0x800405A8, 0x91: 0x800405D4, 0x92: 0x80040628, 0x93: 0x80040680, 0x94: 0x8004071C,
                0xA0: 0x80040D60, 0xA1: 0x80040E10, 0xA2: 0x80040F00, 0xA3: 0x80040FAC}

    def op(self, code: bytes):
        m = self.m
        PC_WORD = 0x801F0100; PROG = 0x801F0000; CODE = 0x801F0200
        for i, c in enumerate(code): m.wb(CODE + i, c)
        m.ww(PC_WORD, CODE)
        v = m.call(self.HANDLERS[code[0]], PROG, 0, PC_WORD)
        return v & 0xFFFFFFFF

    def set_entity(self, key, addr, x, y, z, status=1, depthkey=0):
        m = self.m
        m.ww(addr + 0x10, status); m.ww(addr + 0x114, x); m.ww(addr + 0x118, y); m.ww(addr + 0x11C, z); m.ww(addr + 0x1BC, depthkey)
        self.entities[key] = addr

    # ---- native creators ------------------------------------------------------------------------------------------
    def _stack(self, *vals):
        base = 0x801FFF10
        for i, v in enumerate(vals):
            self.m.ww(base + 4 * i, v & 0xFFFFFFFF)

    def create_free(self, bank, sprite, anim, x, y, z):          # 0x8003BDD8 (mode 0)
        # a0 bank, a1 sprite, a2 anim, a3 x ; sp+0x10 y, sp+0x14 z
        self._stack(y, z)
        return self.m.call(0x8003BDD8, bank, sprite, anim, x)

    def create_attached(self, bank, sprite, anim, ent, depth_off, ox, oy, oz):    # 0x8003BE74 (mode 1)
        self._stack(depth_off, ox, oy, oz)
        return self.m.call(0x8003BE74, bank, sprite, anim, ent)

    def create_detached(self, bank, sprite, anim, ent, depth_off, x, y, z):       # 0x8003BFE8 (mode 3)
        self._stack(depth_off, x, y, z)
        return self.m.call(0x8003BFE8, bank, sprite, anim, ent)
