"""Model of the effect reservoir of ALUN_CD.EXE (France), written from the binary (0x8003B9C4-0x8003C504, 0x800405A8-0x80041094),
driven by the DATA the converter will export (fx_all.json = the JSON of data-extracted), not by the memory of the binary.
Pure python, integers only, 32-bit wrap where the binary wraps. This is the oracle the C# tests can copy."""
import json, os

M32 = 0xFFFFFFFF


def s32(x):
    x &= M32
    return x - (1 << 32) if x & 0x80000000 else x


def u32(x):
    return x & M32


def s16(x):
    x &= 0xFFFF
    return x - 0x10000 if x & 0x8000 else x


SLOTS = 128
BLOCK_MASK = 0x48


class Anim:
    """frames: list of (delay_byte (0..127), set_index or None); term: 0 destroy / 1 loop"""

    def __init__(self, frames, term):
        self.frames = frames
        self.term = term


class Effect:
    """One effect sprite record: anims by index (None = offset 0 = padding slot) and image sets by index."""

    def __init__(self, anims, sets):
        self.anims = anims
        self.sets = sets    # sets: set_index -> (bias, nimages)


class Bank:
    def __init__(self, effects):
        self.effects = effects

    def lookup(self, i):
        if 0 <= i < len(self.effects):
            return self.effects[i]
        return None


class Record:
    __slots__ = ("x1", "y1", "x2", "y2", "flags", "effect", "x", "y", "z", "anim")

    def __init__(self, x1, y1, x2, y2, flags, effect, x, y, z, anim):
        self.x1, self.y1, self.x2, self.y2, self.flags, self.effect, self.x, self.y, self.z, self.anim = x1, y1, x2, y2, flags, effect, x, y, z, anim


class Entity:
    def __init__(self, status=1, x=0, y=0, z=0, depth=0):
        self.status = status
        self.x = x
        self.y = y
        self.z = z
        self.depth = depth


class Slot:
    def __init__(self, idx):
        self.idx = idx
        self.reset()

    def reset(self):
        self.status = 0
        self.rec = None
        self.recidx = 0
        self.mode = 0
        self.ent = None
        self.x = self.y = self.z = 0
        self.off = (0, 0, 0)
        self.forces = (0, 0, 0)
        self.doff = 0
        self.depth = 0
        self.tb = 0
        self.cb = 0
        self.ts = 0
        self.cs = 0
        self.ta = 0
        self.ca = 0
        self.fx = None
        self.anim = None
        self.frame = 0
        self.cnt = 0
        self.dead = 0
        self.set = None
        self.bias = 0
        self.n = 0
        self.pagebase = 0
        self.clutbase = 0
        self.rx = self.ry = self.rz = 0
        self.rdepth = 0


class Pool:
    def __init__(self, map_bank, global_bank, records, hero_tile=(0, 0)):
        self.slots = [Slot(i) for i in range(SLOTS)]
        self.map_bank = map_bank
        self.global_bank = global_bank
        self.records = records
        self.hero_tile = hero_tile
        self.blocked = 0
        self.list = []                      # slots pushed in the last tick, in push order
        self.entities = {}                  # search key -> Entity

    # ---- allocation (0x8003B9C4) --------------------------------------------------------------------------
    def get_free(self):
        for s in self.slots:
            if s.status == 0:
                return s
        return None

    # ---- InitEffect (0x8003BB14) ----------------------------------------------------------------------------
    def init(self, s, rec, recidx, mode, bank, sprite, anim, x, y, z):
        idx = s.idx
        s.reset()
        s.idx = idx                       # template copy (zero) keeping +0
        s.rec = rec
        s.recidx = recidx if rec is not None else -1
        s.mode = mode
        s.status = 2
        s.ts = sprite & 0xFF
        s.ta = anim & 0xFF
        s.tb = 1 if bank else 0
        s.cb = s.tb ^ 1
        s.cs = (~sprite) & 0xFF
        s.ca = (~anim) & 0xFF
        s.x, s.y, s.z = s32(x), s32(y), s32(z)

    # ---- SpawnMapEffect (0x8003C094) + GetMapEffectRecord (0x8003BA70) ----------------------------------------
    def spawn_record(self, index, force):
        if not (0 <= index < len(self.records)):
            return None
        r = self.records[index]
        if not force:
            tx, ty = self.hero_tile
            if tx < r.x1 or r.x2 < tx or ty < r.y1 or r.y2 < ty:
                return None
            if not (r.flags & 0x40):
                return None
        s = self.get_free()
        if s is None:
            return None
        self.init(s, r, index, 0, r.flags & 0x80, r.effect, r.anim,
                  (r.x * 12 + 12) << 16, (r.y * 8 + 8) << 16, r.z << 19)
        return s

    def map_load(self):                              # 0x8003C1A4
        for s in self.slots:
            s.status = 0
        for i in range(len(self.records)):
            self.spawn_record(i, 0)

    # ---- tick (0x8003C410) ---------------------------------------------------------------------------------
    def tick(self, blocked=0):
        self.blocked = blocked
        self.list = []
        for s in self.slots:
            if s.status != 2:
                continue
            if (blocked & BLOCK_MASK) == 0:
                if s.dead:
                    s.status = 0
                    continue
                self.animate(s)
                self.move(s)
            s.rdepth = u32(s.depth)
            s.rx, s.ry, s.rz = s.x, s.y, s.z
            self.list.append(s)
        return self.list

    # ---- animation (0x8003BBDC) ---------------------------------------------------------------------------
    def bank_of(self, tb):
        return self.map_bank if tb else self.global_bank

    def animate(self, s):
        if s.ts != s.cs or s.tb != s.cb:
            fx = self.bank_of(s.tb).lookup(s.ts)
            if fx is None:
                s.dead = 1
                s.set = None
                s.bias = 0
                s.n = 0
                return
            s.fx = fx
            s.pagebase = 0 if s.tb else 0xB
            s.clutbase = 0x20 if s.tb else 0x60
            s.cb = s.tb
            s.cs = s.ts
            s.ca = (~s.ta) & 0xFF
        if s.ta != s.ca:
            s.anim = s.fx.anims[s.ta] if s.ta < len(s.fx.anims) else None
            if s.anim is None:
                raise RuntimeError("animation index %d outside the record or padding slot" % s.ta)
            s.ca = s.ta
            s.frame = 0
            s.cnt = 0
            s.dead = 0
        else:
            s.cnt = (s.cnt - 1) & 0xFF
            if s.cnt != 0:
                return
        guard = 0
        while True:
            guard += 1
            if guard > 4:
                raise RuntimeError("loop animation without frames")
            a = s.anim
            if s.frame < len(a.frames):
                d, setidx = a.frames[s.frame]
                s.frame += 1
                s.cnt = d
                if setidx is None:
                    s.set = None
                    s.bias = 0
                    s.n = 0
                else:
                    s.set = setidx
                    s.bias, s.n = s.fx.sets[setidx]
                return
            if a.term == 0:
                s.cnt = 0xFF
                s.dead = 1
                return
            s.frame = 0                                # LOOP: back to the first frame

    # ---- position (0x8003C284) ----------------------------------------------------------------------------
    def move(self, s):
        m = s.mode
        if m == 0:
            s.x = s32(s.x + s.forces[0])
            s.z = s32(s.z + s.forces[2])
            s.y = s32(s.y + s.forces[1])
            s.depth = u32((s.y & 0xFFFF0000) + (s.z >> 16) + ((s.bias & 0xFFFF) << 16))
            return
        if m == 2:
            return
        if m == 1:
            e = s.ent
            if e.status == 0:
                s.mode = 2
                m = 3                        # 0x8003C398 stores mode 2 then falls into 0x8003C39C (mode 3 body)
            else:
                s.x = s32(e.x + s.off[0])
                s.y = s32(e.y + s.off[1])
                s.z = s32(e.z + s.off[2])
                s.depth = u32(e.depth + s.doff)
                if e.status == 4:
                    s.mode = 2
                m = 3
        if m == 3:
            e = s.ent
            s.x = s32(s.x + s.forces[0])
            s.y = s32(s.y + s.forces[1])
            s.z = s32(s.z + s.forces[2])
            if e.status == 0:
                s.mode = 2
                return
            s.depth = u32(e.depth + s.doff)
            if e.status == 4:
                s.mode = 2

    # ---- native creators (0x8003BDD8 mode 0, 0x8003BE74 mode 1, 0x8003BFE8 mode 3) ------------------------------------
    def create_free(self, bank, sprite, anim, x, y, z):
        s = self.get_free()
        if s is None:
            return None
        self.init(s, None, -1, 0, bank, sprite, anim, x, y, z)
        return s

    def create_attached(self, bank, sprite, anim, ent, depth_off, ox, oy, oz):
        s = self.get_free()
        if s is None:
            return None
        self.init(s, None, -1, 1, bank, sprite, anim, ent.x, ent.y, ent.z)
        s.ent = ent
        s.doff = s32(depth_off)
        s.off = (s32(ox), s32(oy), s32(oz))
        return s

    def create_detached(self, bank, sprite, anim, ent, depth_off, x, y, z):
        s = self.get_free()
        if s is None:
            return None
        self.init(s, None, -1, 3, bank, sprite, anim, x, y, z)
        s.ent = ent
        s.doff = s32(depth_off)
        return s

    # ---- opcodes (0x800405A8-0x80041094) -------------------------------------------------------------------
    def op(self, code):
        o = code[0]
        b1 = code[1] if len(code) > 1 else 0

        def w(i):
            return code[i] | (code[i + 1] << 8)
        if o == 0x90:
            self.spawn_record(b1, 1)
            return 2
        if o == 0x91:
            for s in self.slots:
                if s.status != 0 and s.recidx == b1:
                    s.status = 0
            return 2
        if o == 0x92:
            for s in self.slots:
                if s.status != 0 and s.recidx == b1:
                    s.ta = code[2]
            return 3
        if o == 0x93:
            x, y, z = u32(w(2) << 16), u32(w(4) << 16), u32((w(6) << 16) + 1)
            for s in self.slots:
                if s.status != 0 and s.recidx == b1:
                    s.x, s.y, s.z = s32(x), s32(y), s32(z)
            return 8
        if o == 0x94:
            f = (s32(s16(w(2)) << 8), s32(s16(w(4)) << 8), s32(s16(w(6)) << 8))
            for s in self.slots:
                if s.status != 0 and s.recidx == b1:
                    s.forces = f
            return 8
        if o == 0xA0:
            dx, dy, dz = u32(w(2) << 16), u32(w(4) << 16), u32(w(6) << 16)
            for s in self.slots:
                if s.status != 0 and s.recidx == b1:
                    s.x = s32(s.x + dx)
                    s.y = s32(s.y + dy)
                    s.z = s32(s.z + dz)
            return 8
        if o == 0xA1:
            e = self.entities.get(code[2])
            if e is not None:
                dx, dy, dz = u32(w(3) << 16), u32(w(5) << 16), u32(w(7) << 16)
                for s in self.slots:
                    if s.status != 0 and s.recidx == b1:
                        s.x = s32(e.x + dx)
                        s.y = s32(e.y + dy)
                        s.z = s32(e.z + dz)
            return 9
        if o == 0xA2:
            s = self.spawn_record(b1, 1)
            if s is not None:
                s.x, s.y, s.z = s32(w(2) << 16), s32(w(4) << 16), s32((w(6) << 16) + 1)
            return 8
        if o == 0xA3:
            e = self.entities.get(code[2])
            if e is not None:
                s = self.spawn_record(b1, 1)
                if s is not None:
                    s.x, s.y, s.z = s32(e.x + u32(w(3) << 16)), s32(e.y + u32(w(5) << 16)), s32(e.z + u32(w(7) << 16))
            return 9
        raise ValueError(o)


# ---- builders from fx_all.json ----------------------------------------------------------------------------------
def build_bank(fxmap):
    """fxmap = one entry of fx_all.json -> Bank. Padding slots (offset 0) become None anims."""
    effs = []
    for e in fxmap["effects"]:
        anims = []
        sets = {}
        for ai, a in enumerate(e["anims"]):
            off = e["offsets"][ai] if ai < len(e["offsets"]) else None
            if off == 0:
                anims.append(None)
                continue
            fr = a["frames"]
            last = fr[-1]
            frames = []
            for f in fr[:-1]:
                assert f["d"] & 0x80
                setidx = None if f["ptr"] in (-1, 0xFFFF) else f["ptr"]
                if setidx is not None:
                    sets[setidx] = (f["bias"], f["n"])
                frames.append((f["d"] & 0x7F, setidx))
            assert not (last["d"] & 0x80) and last["d"] in (0, 1), last
            anims.append(Anim(frames, last["d"]))
        effs.append(Effect(anims, sets))
    return Bank(effs)


def load_all(path=None):
    path = path or os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "data", "fx_all.json")
    return json.load(open(path))


def records_of(fxmap):
    return [Record(*r[:10]) for r in fxmap["records"]]
