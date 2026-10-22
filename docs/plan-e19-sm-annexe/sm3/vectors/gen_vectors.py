"""E19.sm3 value vectors: the REAL handlers of ALUN_CD.EXE (France) for 0xA9 0xAA 0xB2 0xB4 0xB5 0xB6 0xB7 0x9E, run in the minimal MIPS-I
interpreter of the sm1-sm4 annex (a COPY of work/oracle/{oracle,emu,lib}.py, tools/oracle/), on explicit worlds (hand-written edges, a sweep of
every search type, seeded random rows), and frozen as replayable JSON lines.  READ-ONLY on the EXE and on the repository; writes only next to itself.

Usage:  python -B gen_vectors.py OUT_DIR           (deterministic: two runs give byte-identical files)

For each vector the script records the world the handler READS (slots, N), the instruction bytes, v0, the Result word written (or None), every word
written outside the scratch (none expected), the set of memory reads mapped to DLL members (an unmapped read is a STOP: the script exits 2), the
agreement of the python model written from the disassembly (oracle.MODELS) with the binary run, and the agreement of the DLL-shaped model
(oracle.dll_search = the python translation of EntitySearchService.cs) with the binary run.
"""
import hashlib
import json
import os
import random
import struct
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, '..', 'tools', 'oracle'))
import emu          # noqa: E402
import oracle       # noqa: E402
from oracle import ent_addr, ENT0, STRIDE, NADDR, RES, STATE, CODE, PCP, TABLE, MAPPTR, MAPBASE, M32, s32  # noqa: E402

OPS = (0xA9, 0xAA, 0xB2, 0xB4, 0xB5, 0xB6, 0xB7, 0x9E)
SIZES = {0xA9: 2, 0xAA: 2, 0xB2: 3, 0xB4: 2, 0xB5: 2, 0xB6: 2, 0xB7: 3, 0x9E: 6}
HERO_ADDR = ent_addr(0)
RESULT_SENTINEL = 7           # the convention of the E19.l tests: the Result before the instruction is 7 (never 0 or 1)
GRID_W, GRID_H = 52, 60
CELL_BASE = MAPBASE + 0x604

# JSON field -> (offset in the 0x294-byte entity, kind, DLL member)
FIELDS = {
    'parent':      (0x0C, 'ptr', 'EntityScriptProxy.ParentEntity (Entity?, compared with the reference LogicContextEntity)'),
    'status':      (0x10, 'u32', 'EntityScriptProxy.Status (1 Loaded, 2 Normal, 3 Deactivated, 4 FlagToDestroy; IsLoadedNormalOrDeactivated = 1..3)'),
    'platform':    (0x28, 'ptr', 'EntityScriptProxy.PlatformEntity (Entity?; only the null test is read)'),
    'ref_id':      (0x48, 'i32', 'EntityScriptProxy.EntityRefId (int; the hero is -1)'),
    'flags':       (0x6C, 'u32', 'EntityScriptProxy.Flags (EntityFlags.Collidable = 0x80)'),
    'target_anim': (0x88, 'u32', 'EntityScriptProxy.TargetAnimationId (uint)'),
    'anim_flags':  (0xB4, 'u32', 'EntityScriptProxy.AnimFlags (NoEntityCollision = 0x80, local constant of EntitySearchService)'),
    'force_z':     (0xB8, 'i32', 'EntityScriptProxy.ForceZ (int, signed)'),
    'riding':      (0x12C, 'ptr', 'EntityScriptProxy.RidingEntity (Entity?)'),
    'xcoll':       (0x130, 'ptr', 'EntityScriptProxy.XCollisionEntity (EntityScriptProxy?)'),
}
OFF2FIELD = {v[0]: k for k, v in FIELDS.items()}
ORACLE_KEY = {'ref_id': 'ref', 'target_anim': 'tanim', 'anim_flags': 'anim', 'force_z': 'forcez'}   # oracle.World dict keys that differ


class TEmu(emu.Emu):
    """emu.Emu with a log of the DATA reads and writes (instruction fetches are not logged)."""

    def __init__(self):
        super().__init__()
        self.rlog = None
        self.wlog = None

    def rb(self, a):
        if self.rlog is not None:
            self.rlog.append((a & M32, 1, self.pc))
        return super().rb(a)

    def rh(self, a):
        if self.rlog is not None:
            self.rlog.append((a & M32, 2, self.pc))
        return super().rh(a)

    def rw(self, a):
        if self.rlog is not None:
            self.rlog.append((a & M32, 4, self.pc))
        return super().rw(a)

    def wb(self, a, v):
        if self.wlog is not None:
            self.wlog.append((a & M32, 1, self.pc))
        super().wb(a, v)

    def wh(self, a, v):
        if self.wlog is not None:
            self.wlog.append((a & M32, 2, self.pc))
        super().wh(a, v)

    def ww(self, a, v):
        if self.wlog is not None:
            self.wlog.append((a & M32, 4, self.pc))
        super().ww(a, v)

    def step(self):
        pc = self.pc
        w = emu.Emu.rw(self, pc)
        br = self.exec1(pc, w)
        if br is None:
            self.pc = pc + 4
        else:
            dw = emu.Emu.rw(self, pc + 4)
            self.exec1(pc + 4, dw)
            self.pc = br[1] if br[1] is not None else pc + 8


def make_machine():
    m = TEmu()
    m.hooks[0x8003C914] = lambda mm: None      # raw-id validator: its result is discarded by every caller (fatal on an invalid id: INCONNU, see the README)
    m.hooks[0x8003C840] = lambda mm: None      # debug printer of the illegal-search-type path (the binary exits there: not a value)
    return m


# ------------------------------------------------------------------------------------------------------------------------------ world spec
def slot(status=2, ref_id=0, flags=0x80, anim_flags=0, platform=None, riding=None, xcoll=None, parent=None, force_z=0, target_anim=0):
    return dict(status=status, ref_id=ref_id, flags=flags, anim_flags=anim_flags, platform=platform, riding=riding, xcoll=xcoll, parent=parent,
                force_z=force_z, target_anim=target_anim)


def hero(**kw):
    kw.setdefault('ref_id', -1)
    return slot(**kw)


def free_slot():
    return slot(status=0, ref_id=0, flags=0)


def write_world(m, spec):
    slots = spec['slots']
    n_last = spec.get('n_last', len(slots) - 1)
    for off in range(0, 66 * STRIDE, 4):
        m.ww(ENT0 + off, 0)
    m.ww(NADDR, n_last)
    for i, s in enumerate(slots):
        a = ent_addr(i)
        for k, (off, kind, _) in FIELDS.items():
            v = s[k]
            if kind == 'ptr':
                m.ww(a + off, 0 if v is None else ent_addr(v))
            else:
                m.ww(a + off, v & M32)
    for off in range(0, 0x40, 4):
        m.ww(STATE + off, 0)
    for off in range(0, 0x120, 4):
        m.ww(RES - 4 + off, 0xDEADBEEF)
    m.ww(STATE + 0x2C, spec.get('result_before', RESULT_SENTINEL) & M32)
    instr = spec['instr']
    code = bytes(instr) + bytes(16 - len(instr))
    for i, x in enumerate(code):
        m.wb(CODE + i, x)
    m.ww(PCP, CODE)
    if spec.get('cells') is not None or spec['instr'][0] == 0x9E:
        m.ww(MAPPTR, MAPBASE)
        grid = spec.get('grid_seed')
        rng = random.Random(grid) if grid is not None else None
        for y in range(GRID_H):
            for x in range(GRID_W):
                v = rng.choice([0, 0, 1, 2, 3, 0x100, 0x102, 0x4, 0x6, 0x8]) if rng else 0
                m.wh(CELL_BASE + (y * GRID_W + x) * 8, v)
        for (x, y, v) in spec.get('cells', []):
            m.wh(CELL_BASE + (y * GRID_W + x) * 8, v)


class LinearCells:
    """the cell array as the binary addresses it: ONE linear index y*52+x (a column >= 52 runs into the next row); the model reads (row, col)."""

    def __init__(self, byxy):
        self.byxy = byxy

    def get(self, key, default=0):
        row, col = key
        lin = row * GRID_W + col
        return self.byxy.get(divmod(lin, GRID_W), default)


def dll_search2(w, self_i, t):
    """oracle.dll_search (python translation of EntitySearchService.cs) over the candidates the DLL really has: no free slot (status 0) in SpawnedEntities."""
    saved = w.n
    keep = [c for c in range(w.n) if c == 0 or w.e[c]['status'] != 0]
    # reuse oracle.dll_search on a compacted view: indices are remapped back afterwards
    class View:
        pass
    v = View()
    v.n = len(keep)
    v.e = [w.e[c] for c in keep]
    si = keep.index(self_i) if self_i in keep else None
    if si is None:
        # the reference itself is a free slot: the DLL still runs a program on it; give it a private view slot
        keep2 = keep + [self_i]
        v.n = len(keep2)
        v.e = [w.e[c] for c in keep2]
        si = len(keep2) - 1
        keep = keep2
    # the pointer fields hold addresses of ORIGINAL slots: translate the reference/candidate addresses by comparing against original indexes
    res = []
    S = w.e[self_i]
    a = ent_addr(self_i)
    pl = 0
    ft = t & 0x7F
    if (t & 0x80) == 0:
        if 1 <= S['status'] <= 3:
            return [c for c in keep if w.e[c]['ref'] == t]
        return []
    if ft == 0:
        return [self_i]
    if ft == 1:
        return [pl]
    for c in keep:
        d = w.e[c]
        ca = ent_addr(c)
        act = 1 <= d['status'] <= 3
        if ft == 2 and act:
            res.append(c)
        elif ft == 3 and act and c != pl:
            res.append(c)
        elif ft == 4 and 1 <= S['status'] <= 3 and (d['flags'] & 0x80) and not (d['anim'] & 0x80) and d['platform'] == 0:
            res.append(c)
        elif ft == 5 and c != pl and act and S['riding'] == ca:
            res.append(c)
        elif ft == 6 and c != pl and act and d['riding'] == a:
            res.append(c)
        elif ft == 7 and c != pl and act and S['xcoll'] == ca:
            res.append(c)
        elif ft == 8 and c != pl and act and d['xcoll'] == a:
            res.append(c)
        elif ft == 9 and c != pl and act and d['parent'] == a:
            res.append(c)
        elif ft == 10 and c != pl and act and S['parent'] == ca:
            res.append(c)
        elif ft == 11 and c != pl and act and d['platform'] != 0:
            res.append(c)
    return res


class W:
    """adapter: the spec as the `w` object the oracle models read (addresses for pointers)."""

    def __init__(self, spec):
        slots = spec['slots']
        self.n = spec.get('n_last', len(slots) - 1) + 1
        self.e = []
        for s in slots:
            d = {}
            for k, (off, kind, _) in FIELDS.items():
                v = s[k]
                d[ORACLE_KEY.get(k, k)] = (0 if v is None else ent_addr(v)) if kind == 'ptr' else (v & M32)
            self.e.append(d)
        while len(self.e) < self.n:
            self.e.append({k: 0 for k in ('parent', 'status', 'platform', 'ref', 'flags', 'tanim', 'anim', 'forcez', 'riding', 'xcoll')})
        self.memo = [0, 0, 0]
        self.pstats = {}
        byxy = {}
        self.grid_seed = spec.get('grid_seed')
        if self.grid_seed is not None:
            rng = random.Random(self.grid_seed)
            for y in range(GRID_H):
                for x in range(GRID_W):
                    byxy[(y, x)] = rng.choice([0, 0, 1, 2, 3, 0x100, 0x102, 0x4, 0x6, 0x8])
        for (x, y, v) in spec.get('cells', []):
            byxy[(y, x)] = v
        self.cells = LinearCells(byxy)


def classify_read(a, size, spec):
    """Maps a data read to a (key, member) pair, or None when unmapped."""
    if 0x801FFC00 <= a < 0x80200000:
        return ('stack', 'callee-saved registers and the frame of the handler (the copy of B2 lives there): not world data')
    if ENT0 <= a < ENT0 + 66 * STRIDE:
        i, off = divmod(a - ENT0, STRIDE)
        base = off & ~3
        if i >= len(spec['slots']) + 0 and i > spec.get('n_last', len(spec['slots']) - 1):
            return None
        if base in OFF2FIELD:
            f = OFF2FIELD[base]
            return ('slot.' + f, FIELDS[f][2])
        return None
    if a == NADDR:
        return ('n_last', 'no member: N is the index of the last used slot; the DLL searches the entries of IEntityWorldContext.SpawnedEntities (hero first, then the slots in order)')
    if RES - 4 <= a < RES - 4 + 0x120:
        return ('match_buffer', 'the list returned by EntitySearchService.GetMatchingEntitiesBySearchType (written by the search in the same call)')
    if a == PCP:
        return ('pc_pointer', 'EventProgramState.CodeIndex (the pointer to the instruction)')
    if CODE <= a < CODE + 16:
        return ('code_byte[%d]' % (a - CODE), 'variables[%d] (FillDataFromCommand: the instruction bytes, zero-extended ints)' % (a - CODE))
    if 0x80023C30 <= a < 0x80023C60:
        return ('search_jump_table', 'the switch (searchType & 0x7F) of EntitySearchService (constant table of the binary, no data)')
    if a == MAPPTR:
        return ('map_pointer', 'the current map data, base of the cell array (the DLL: the CellStore behind IEntityWorldContext.CellMutator)')
    if CELL_BASE <= a < CELL_BASE + GRID_W * GRID_H * 8:
        cell, off = divmod(a - CELL_BASE, 8)
        if off in (0, 1):
            return ('cell.walkability_halfword', 'CellStore walkability byte (the low byte of the halfword at cell+0, bit 1 tested) [high byte = GroundProperty, masked out]')
        return None
    return None


def run_vector(spec, check_models=True):
    m = make_machine()
    write_world(m, spec)
    op = spec['instr'][0]
    ref = spec['ref']
    handler = m.rw(TABLE + 4 * op)
    m.rlog = []
    m.wlog = []
    v0 = m.call(handler, ent_addr(ref), ent_addr(ref), PCP, STATE)
    rlog, wlog = m.rlog, m.wlog
    m.rlog = m.wlog = None
    result = m.rw(STATE + 0x2C)
    result_written = any(a == STATE + 0x2C and sz == 4 for a, sz, _ in wlog)
    unmodeled = []
    writes_other = sorted({a for a, sz, _ in wlog
                           if not (0x801FFC00 <= a < 0x80200000) and not (RES - 4 <= a < RES - 4 + 0x120) and not (STATE + 0x2C <= a < STATE + 0x30)})
    # reads: classified; the match buffer must only be read after the same call wrote it
    reads = {}
    unmapped = []
    written_buffer = set()
    for a, sz, _ in wlog:
        if RES - 4 <= a < RES - 4 + 0x120:
            written_buffer.add(a & ~3)
    for a, sz, rpc in rlog:
        c = classify_read(a, sz, spec)
        if c is None:
            unmapped.append('%08x/%d@%08x' % (a, sz, rpc))
            continue
        if c[0] == 'match_buffer' and (a & ~3) not in written_buffer:
            if op == 0xB2 and 0x80041688 <= rpc <= 0x800416B4:
                c = ('match_buffer_copy', 'B2 only: the copy loop 0x80041688-0x800416B0 copies the 65 words of the first result list to the stack; the words beyond n1 are copied but never compared (the compare loop is bounded by n1)')
            else:
                unmapped.append('%08x/%d@%08x(unwritten buffer word)' % (a, sz, rpc))
        key = c[0]
        if key.startswith('slot.'):
            i = (a - ENT0) // STRIDE
            key = 'slot[%d].%s' % (i, key[5:])
        reads.setdefault(key, c[1])
    out = dict(v0=v0, result=(result if result_written else None), result_written=result_written, writes_other=['%08x' % a for a in writes_other],
               unmodeled=['%08x' % a for a in sorted(set(unmodeled))], reads=reads, unmapped=unmapped, steps=m.steps)
    if check_models:
        w = W(spec)
        w.pstats = dict(hp=0, hpmax=0, mp=0, mpmax=0, money=0)
        w.rng = random.Random(0)
        b = [op] + list(spec['instr'][1:]) + [0] * (10 - len(spec['instr']))
        exp = oracle.MODELS[op](w, ref, b, oracle.model_search)
        out['model_binary_agrees'] = (exp.v0 == v0 and exp.result == out['result'])
        out['model_binary'] = dict(v0=exp.v0, result=exp.result)
        dexp = oracle.MODELS[op](w, ref, b, dll_search2)
        out['dll_shape'] = dict(v0=dexp.v0, result=dexp.result)
        out['dll_shape_agrees'] = (dexp.v0 == v0 and dexp.result == out['result'])
    return out


# ------------------------------------------------------------------------------------------------------------------------------ hand-written
def V(vid, op, instr, ref, slots, **kw):
    d = dict(id=vid, op=op, instr=list(instr), ref=ref, slots=slots)
    d.update(kw)
    return d


def hand_vectors():
    out = []
    # the real sites decide the shapes: AA 81 / A9 81 are the group of the hero (type 1), A9 18 a raw id, B2 xx 8B raw id + type 11, B5 81 / B5 80 / B6 80 the hero / self.
    # ---------------------------------------------------------------- A9: I am blocked by an entity of the group (logic.XCollisionEntity is among the matches)
    def a9(vid, t, slots, ref, note, rb=RESULT_SENTINEL, **kw):
        out.append(V(vid, 0xA9, [0xA9, t], ref, slots, note=note, result_before=rb, **kw))

    a9('A9-01-hero-blocks-me', 0x81, [hero(), slot(ref_id=1, xcoll=0)], 1, 'real shape of 440 C[16] A9 81: the logic entity (slot 1) is blocked by the hero')
    a9('A9-02-null-contact', 0x81, [hero(), slot(ref_id=1, xcoll=None)], 1, 'XCollisionEntity null never matches (the group holds the hero)')
    a9('A9-03-other-contact', 0x81, [hero(), slot(ref_id=1, xcoll=2), slot(ref_id=2)], 1, 'blocked by another entity than the group (the hero)')
    a9('A9-04-rawid24-hero-is-reference', 0x18, [hero(xcoll=2), slot(ref_id=5), slot(ref_id=24)], 0,
       'real shape of 440 B[9] @628 A9 18: the reference is the hero (B program), the group is the raw id 24; the hero is blocked by it')
    a9('A9-05-rawid-no-entity', 0x18, [hero(xcoll=1), slot(ref_id=5)], 0, 'raw id with no entity of that id: group empty')
    a9('A9-06-rawid-reference-not-active', 0x18, [hero(), slot(ref_id=5, status=4, xcoll=2), slot(ref_id=24)], 1,
       'the raw-id search needs the REFERENCE to be Loaded/Normal/Deactivated (status 4 gives an empty group)')
    a9('A9-07-rawid-reference-loaded', 0x18, [hero(), slot(ref_id=5, status=1, xcoll=2), slot(ref_id=24)], 1, 'reference Loaded (1): the raw-id search runs')
    a9('A9-08-rawid-reference-deactivated', 0x18, [hero(), slot(ref_id=5, status=3, xcoll=2), slot(ref_id=24)], 1, 'reference Deactivated (3): the raw-id search runs')
    a9('A9-09-rawid-candidate-status-ignored', 0x18, [hero(), slot(ref_id=5, xcoll=2), slot(ref_id=24, status=4)], 1,
       'the raw-id search does not test the status of the CANDIDATE (a status-4 entity of that id is found)')
    a9('A9-10-rawid-second-of-two', 0x18, [hero(), slot(ref_id=5, xcoll=3), slot(ref_id=24), slot(ref_id=24)], 1, 'two entities of the id: the contact is the second')
    a9('A9-11-self-group', 0x80, [hero(), slot(ref_id=1, xcoll=1)], 1, 'type 0 (the reference itself): blocked by myself')
    a9('A9-12-self-group-other', 0x80, [hero(), slot(ref_id=1, xcoll=0)], 1, 'type 0: the contact is the hero, not me')
    a9('A9-13-all-contact-destroyed', 0x82, [hero(), slot(ref_id=1, xcoll=2), slot(ref_id=2, status=4)], 1, 'type 2 needs a status 1..3: a status-4 contact is not in the group')
    a9('A9-14-all-contact-loaded', 0x82, [hero(), slot(ref_id=1, xcoll=2), slot(ref_id=2, status=1)], 1, 'type 2: a Loaded contact is in the group')
    a9('A9-15-all-contact-deactivated', 0x82, [hero(), slot(ref_id=1, xcoll=2), slot(ref_id=2, status=3)], 1, 'type 2: a Deactivated contact is in the group')
    a9('A9-16-all-but-hero-hero-contact', 0x83, [hero(), slot(ref_id=1, xcoll=0), slot(ref_id=2)], 1, 'type 3 excludes the hero: a hero contact does not match')
    a9('A9-17-all-but-hero-npc-contact', 0x83, [hero(), slot(ref_id=1, xcoll=2), slot(ref_id=2)], 1, 'type 3: an NPC contact matches')
    a9('A9-18-type11-carried-contact', 0x8B, [hero(), slot(ref_id=1, xcoll=2), slot(ref_id=2, platform=0)], 1, 'type 11 (carried): a carried entity is the contact')
    a9('A9-19-type11-not-carried', 0x8B, [hero(), slot(ref_id=1, xcoll=2), slot(ref_id=2, platform=None)], 1, 'type 11: the contact is not carried')
    a9('A9-20-empty-group', 0x83, [hero(xcoll=0)], 0, 'only the hero exists: type 3 (all but the hero) is empty')
    a9('A9-21-stale-one-then-false', 0x81, [hero(), slot(ref_id=1, xcoll=None)], 1, 'the Result is overwritten on a false (stale 1 becomes 0)', rb=1)
    a9('A9-22-stale-zero-then-true', 0x81, [hero(), slot(ref_id=1, xcoll=0)], 1, 'the Result is overwritten on a true (stale 0 becomes 1)', rb=0)
    a9('A9-23-hero-reference-self-contact', 0x81, [hero(xcoll=0)], 0, 'the logic entity is the hero, whose contact is itself')
    a9('A9-24-hero-reference-group-hero-null', 0x81, [hero(xcoll=None), slot(ref_id=1)], 0, 'the logic entity is the hero, null contact')
    a9('A9-25-holes-in-the-slots', 0x81, [hero(), free_slot(), free_slot(), slot(ref_id=3, xcoll=0)], 3, 'free slots (status 0) between the used ones: no effect')
    # ---------------------------------------------------------------- AA: an entity of the group is blocked by me (a match's XCollisionEntity is the logic entity)
    def aa(vid, t, slots, ref, note, rb=RESULT_SENTINEL, **kw):
        out.append(V(vid, 0xAA, [0xAA, t], ref, slots, note=note, result_before=rb, **kw))

    aa('AA-01-hero-blocked-by-me', 0x81, [hero(xcoll=1), slot(ref_id=1)], 1, 'real shape of the 24 sites (424 C[3] @700 ...): the hero\'s contact is the logic entity (the block)')
    aa('AA-02-hero-null-contact', 0x81, [hero(xcoll=None), slot(ref_id=1)], 1, 'the hero has no contact')
    aa('AA-03-hero-other-contact', 0x81, [hero(xcoll=2), slot(ref_id=1), slot(ref_id=2)], 1, 'the hero\'s contact is another entity')
    aa('AA-04-all-last-only', 0x82, [hero(), slot(ref_id=1), slot(ref_id=2), slot(ref_id=3, xcoll=1)], 1, 'type 2: only the last of the group is blocked by me')
    aa('AA-05-all-first-only', 0x82, [hero(xcoll=1), slot(ref_id=1), slot(ref_id=2), slot(ref_id=3)], 1, 'type 2: only the first of the group (the hero) is blocked by me')
    aa('AA-06-all-none', 0x82, [hero(), slot(ref_id=1), slot(ref_id=2, xcoll=3), slot(ref_id=3)], 1, 'type 2: nobody is blocked by me')
    aa('AA-07-all-but-hero-excludes-hero', 0x83, [hero(xcoll=1), slot(ref_id=1), slot(ref_id=2)], 1, 'type 3 excludes the hero: the hero blocked by me does not count')
    aa('AA-08-self', 0x80, [hero(), slot(ref_id=1, xcoll=1)], 1, 'type 0: I am blocked by myself')
    aa('AA-09-self-not', 0x80, [hero(), slot(ref_id=1, xcoll=0)], 1, 'type 0: my contact is the hero, not me')
    aa('AA-10-member-destroyed', 0x82, [hero(), slot(ref_id=1), slot(ref_id=2, status=4, xcoll=1)], 1, 'a status-4 member is not in the group')
    aa('AA-11-member-deactivated', 0x82, [hero(), slot(ref_id=1), slot(ref_id=2, status=3, xcoll=1)], 1, 'a Deactivated member is in the group')
    aa('AA-12-member-loaded', 0x82, [hero(), slot(ref_id=1), slot(ref_id=2, status=1, xcoll=1)], 1, 'a Loaded member is in the group')
    aa('AA-13-rawid-member', 0x18, [hero(), slot(ref_id=1), slot(ref_id=24, xcoll=1)], 1, 'raw id 24: the member\'s contact is me')
    aa('AA-14-rawid-reference-not-active', 0x18, [hero(), slot(ref_id=1, status=4), slot(ref_id=24, xcoll=1)], 1, 'raw id with an inactive reference: empty group')
    aa('AA-15-rawid-candidate-destroyed', 0x18, [hero(), slot(ref_id=1), slot(ref_id=24, status=4, xcoll=1)], 1, 'raw id: the status of the candidate is not tested')
    aa('AA-16-hero-reference-hero-group', 0x81, [hero(xcoll=0)], 0, 'the logic entity is the hero itself')
    aa('AA-17-stale-one-then-false', 0x81, [hero(xcoll=None), slot(ref_id=1)], 1, 'overwritten on a false', rb=1)
    aa('AA-18-stale-zero-then-true', 0x81, [hero(xcoll=1), slot(ref_id=1)], 1, 'overwritten on a true', rb=0)
    aa('AA-19-real-440-shape', 0x81, [hero(xcoll=1), slot(ref_id=1, flags=0x80), slot(ref_id=2), slot(ref_id=3)], 1, '440 C[2] @1140 shape: three blocks, the hero pushes the first (the logic entity of C[2])')
    aa('AA-20-real-440-other-block', 0x81, [hero(xcoll=2), slot(ref_id=1), slot(ref_id=2), slot(ref_id=3)], 1, 'the hero pushes the SECOND block: the first block\'s program sees 0')
    aa('AA-21-type11-member', 0x8B, [hero(), slot(ref_id=1), slot(ref_id=2, platform=0, xcoll=1)], 1, 'type 11: a carried member blocked by me')
    # ---------------------------------------------------------------- B2: do the two groups share an entity (n1 == 0 gives 0 without the second search)
    def b2(vid, t1, t2, slots, ref, note, rb=RESULT_SENTINEL, **kw):
        out.append(V(vid, 0xB2, [0xB2, t1, t2], ref, slots, note=note, result_before=rb, **kw))

    b2('B2-01-real-258-carried', 0x03, 0x8B, [hero(), slot(ref_id=3, platform=0)], 0, 'real shape of 258 B[6] @639 B2 03 8B: the entity of id 3 is carried (PlatformEntity set): intersection')
    b2('B2-02-real-258-not-carried', 0x03, 0x8B, [hero(), slot(ref_id=3, platform=None)], 0, 'the entity of id 3 is not carried: g2 empty')
    b2('B2-03-carried-but-destroyed', 0x03, 0x8B, [hero(), slot(ref_id=3, platform=0, status=4)], 0, 'id 3 is found (status ignored) but type 11 needs a status 1..3: no intersection')
    b2('B2-04-other-carried', 0x03, 0x8B, [hero(), slot(ref_id=3), slot(ref_id=4, platform=0)], 0, 'id 3 not carried while id 4 is: disjoint')
    b2('B2-05-two-same-id-one-carried', 0x03, 0x8B, [hero(), slot(ref_id=3), slot(ref_id=3, platform=0)], 0, 'two entities of id 3, the second carried: intersection')
    b2('B2-06-g1-empty', 0x03, 0x8B, [hero(), slot(ref_id=4, platform=0)], 0, 'no entity of id 3: Result 0 (the second search is not needed)')
    b2('B2-07-reference-not-active', 0x03, 0x8B, [hero(status=4), slot(ref_id=3, platform=0)], 0, 'the raw-id search needs an active reference: g1 empty')
    b2('B2-08-all-but-hero-and-all', 0x83, 0x82, [hero(), slot(ref_id=1), slot(ref_id=2)], 0, 'groups 3 and 2 overlap on the non-hero actives')
    b2('B2-09-hero-vs-all-but-hero', 0x81, 0x83, [hero(), slot(ref_id=1)], 0, 'the hero and the non-hero actives are disjoint')
    b2('B2-10-hero-vs-all', 0x81, 0x82, [hero(), slot(ref_id=1)], 0, 'the hero is in the group of everybody')
    b2('B2-11-g2-empty-nobody-carried', 0x82, 0x8B, [hero(), slot(ref_id=1)], 0, 'g1 non-empty, g2 (carried) empty')
    b2('B2-12-self-self', 0x80, 0x80, [hero(), slot(ref_id=1)], 1, 'type 0 twice: the reference with itself')
    b2('B2-13-self-hero', 0x80, 0x81, [hero(), slot(ref_id=1)], 1, 'the reference (an NPC) against the hero: disjoint')
    b2('B2-14-self-hero-same', 0x80, 0x81, [hero()], 0, 'the reference IS the hero')
    b2('B2-15-stale-one-then-false', 0x03, 0x8B, [hero(), slot(ref_id=3)], 0, 'overwritten on a false', rb=1)
    b2('B2-16-stale-zero-then-true', 0x03, 0x8B, [hero(), slot(ref_id=3, platform=0)], 0, 'overwritten on a true', rb=0)
    b2('B2-17-g1-empty-stale-one', 0x03, 0x8B, [hero(), slot(ref_id=4, platform=0)], 0, 'n1 == 0: Result written 0 (stale 1)', rb=1)
    sixty4 = [hero()] + [slot(ref_id=100 + i) for i in range(1, 64)]
    sixty4[63]['ref_id'] = 9
    b2('B2-18-64-slots-last-only', 0x82, 0x09, sixty4, 0, 'hero + 63 entities: g1 = all 64, g2 = raw id 9 = the LAST slot: found (the 65-word copy of the binary holds the 64 results)')
    s2 = [hero()] + [slot(ref_id=100 + i) for i in range(1, 64)]
    s2[1]['ref_id'] = 9
    b2('B2-19-64-slots-g2-first-g1-all', 0x09, 0x82, s2, 0, 'g1 = raw id 9 (slot 1), g2 = all 64: intersection on the first slot')
    b2('B2-20-types-5-6', 0x85, 0x86, [hero(), slot(ref_id=1, riding=2), slot(ref_id=2, riding=1)], 1,
       'riding groups: g1 = entities I ride on (type 5 = slot 2 since ref.riding = slot 2), g2 = entities riding me (type 6 = slot 2 since slot 2 rides slot 1): intersection {2}')
    b2('B2-21-three-way-partial', 0x82, 0x8B, [hero(), slot(ref_id=1), slot(ref_id=2, platform=0), slot(ref_id=3)], 0, 'g1 = all, g2 = {slot 2}: partial overlap is enough (ANY, not ALL)')
    b2('B2-22-same-ref-id-different-entities', 0x80, 0x8B, [hero(), slot(ref_id=5), slot(ref_id=5, platform=0)], 1,
       'identity, not EntityRefId: the reference (id 5, not carried) and a carried entity of the SAME id 5 are different entities -> 0')
    b2('B2-23-hero-minus-one-vs-carried-minus-one', 0x81, 0x8B, [hero(), slot(ref_id=-1, platform=0)], 0,
       'the hero (id -1) and a carried record-less entity (id -1): different entities -> 0')
    # ---------------------------------------------------------------- B4 / B5 / B6: sign of ForceZ of the FIRST match
    fz_values = [0, 1, -1, 0x7FFFFFFF, -0x80000000, 0x10000, -0x10000, 0xFFFF, -0xFFFF, 0x8000, 0x100, -0x100]
    for op, name in ((0xB4, 'B4'), (0xB5, 'B5'), (0xB6, 'B6')):
        for fz in fz_values:
            tag = ('p' if fz > 0 else 'n' if fz < 0 else 'z') + '%X' % abs(fz)
            out.append(V('%s-hero-fz-%s' % (name, tag), op, [op, 0x81], 1, [hero(force_z=fz), slot(ref_id=1)],
                         note='%s 81: the hero\'s ForceZ = %d (the real sites B5 81)' % (name, fz), result_before=RESULT_SENTINEL))
            out.append(V('%s-self-fz-%s' % (name, tag), op, [op, 0x80], 1, [hero(), slot(ref_id=1, force_z=fz)],
                         note='%s 80: the logic entity\'s ForceZ = %d (the real sites B5 80, B6 80)' % (name, fz), result_before=RESULT_SENTINEL))
        # first match decides (type 3: the non-hero actives, in slot order)
        for order, fzs in (('pos-neg-zero', (5, -5, 0)), ('neg-pos-zero', (-5, 5, 0)), ('zero-pos-neg', (0, 5, -5)), ('zero-zero-zero', (0, 0, 0)), ('neg-neg-neg', (-1, -2, -3))):
            out.append(V('%s-first-match-%s' % (name, order), op, [op, 0x83], 0,
                         [hero(), slot(ref_id=1, force_z=fzs[0]), slot(ref_id=2, force_z=fzs[1]), slot(ref_id=3, force_z=fzs[2])],
                         note='%s 83: ForceZ of slots 1..3 = %s: only the FIRST match counts' % (name, fzs), result_before=RESULT_SENTINEL))
        out.append(V('%s-first-match-skips-inactive' % name, op, [op, 0x83], 0,
                     [hero(), slot(ref_id=1, status=4, force_z=5), slot(ref_id=2, force_z=0), slot(ref_id=3, force_z=-5)],
                     note='%s 83: slot 1 is status 4 (not in the group): the first MATCH is slot 2 (ForceZ 0)' % name, result_before=RESULT_SENTINEL))
        out.append(V('%s-empty-group' % name, op, [op, 0x83], 0, [hero(force_z=5)],
                     note='%s 83 with only the hero: the group is empty -> 0 for all three (B6 too)' % name, result_before=RESULT_SENTINEL))
        out.append(V('%s-rawid-no-entity' % name, op, [op, 0x14], 0, [hero(force_z=5), slot(ref_id=1, force_z=5)],
                     note='%s on a raw id nobody has: empty group' % name, result_before=RESULT_SENTINEL))
        out.append(V('%s-stale-one' % name, op, [op, 0x81], 1, [hero(force_z=(0 if op != 0xB6 else 3)), slot(ref_id=1)],
                     note='%s: false overwrites a stale 1' % name, result_before=1))
        out.append(V('%s-stale-zero' % name, op, [op, 0x81], 1, [hero(force_z=(3 if op == 0xB4 else -3 if op == 0xB5 else 0)), slot(ref_id=1)],
                     note='%s: true overwrites a stale 0' % name, result_before=0))
    # ---------------------------------------------------------------- B7: TargetAnimationId == the operand byte (32-bit word against a zero-extended byte)
    def b7(vid, t, v, slots, ref, note, rb=RESULT_SENTINEL):
        out.append(V(vid, 0xB7, [0xB7, t, v], ref, slots, note=note, result_before=rb))

    b7('B7-01-equal', 0x80, 0x41, [hero(), slot(ref_id=1, target_anim=0x41)], 1, 'equal')
    b7('B7-02-not-equal', 0x80, 0x41, [hero(), slot(ref_id=1, target_anim=0x42)], 1, 'different')
    b7('B7-03-high-bits-differ', 0x80, 0x41, [hero(), slot(ref_id=1, target_anim=0x141)], 1, '32-bit word against a byte: 0x141 != 0x41 (a (byte) cast would match)')
    b7('B7-04-all-ones', 0x80, 0xFF, [hero(), slot(ref_id=1, target_anim=0xFFFFFFFF)], 1, '0xFFFFFFFF != 0xFF (no sign extension of the operand byte)')
    b7('B7-05-byte-ff', 0x80, 0xFF, [hero(), slot(ref_id=1, target_anim=0xFF)], 1, '0xFF == 0xFF')
    b7('B7-06-zero-default', 0x80, 0x00, [hero(), slot(ref_id=1, target_anim=0)], 1, 'a fresh entity (animation 0) against operand 0')
    b7('B7-07-operand-roles', 0x82, 0x07, [hero(), slot(ref_id=7, target_anim=0x82)], 1, 'v1 is the SEARCH TYPE, v2 the value: group 0x82 has no animation 7 (a swapped port would search id 7 and compare 0x82)')
    b7('B7-08-hero-group', 0x81, 0x0C, [hero(target_anim=0x0C), slot(ref_id=1)], 1, 'the hero\'s animation')
    b7('B7-09-last-of-group', 0x83, 0x09, [hero(), slot(ref_id=1), slot(ref_id=2), slot(ref_id=3, target_anim=9)], 0, 'only the last of the group matches (any, not first)')
    b7('B7-10-first-of-group', 0x83, 0x09, [hero(), slot(ref_id=1, target_anim=9), slot(ref_id=2), slot(ref_id=3)], 0, 'only the first matches')
    b7('B7-11-none-of-group', 0x83, 0x09, [hero(), slot(ref_id=1, target_anim=8), slot(ref_id=2, target_anim=10)], 0, 'none matches')
    b7('B7-12-empty-group', 0x14, 0x00, [hero(), slot(ref_id=1)], 0, 'raw id nobody has: empty group -> 0 even for operand 0')
    b7('B7-13-inactive-member', 0x82, 0x09, [hero(), slot(ref_id=1, status=4, target_anim=9)], 0, 'a status-4 member is not in the group')
    b7('B7-14-stale-one', 0x80, 0x41, [hero(), slot(ref_id=1, target_anim=0x42)], 1, 'false overwrites stale 1', rb=1)
    b7('B7-15-stale-zero', 0x80, 0x41, [hero(), slot(ref_id=1, target_anim=0x41)], 1, 'true overwrites stale 0', rb=0)
    # ---------------------------------------------------------------- 9E: count of cells with (halfword & 2) in the rectangle, Result = count >= min
    def c9e(vid, x, y, w, h, mn, cells, note, rb=RESULT_SENTINEL):
        out.append(V(vid, 0x9E, [0x9E, x, y, w, h, mn], 0, [hero()], cells=cells, note=note, result_before=rb))

    c9e('9E-01-one-cell-yes', 3, 4, 1, 1, 1, [(3, 4, 2)], 'one cell with bit 1: count 1 >= 1')
    c9e('9E-02-one-cell-min-2', 3, 4, 1, 1, 2, [(3, 4, 2)], 'count 1 < 2')
    c9e('9E-03-bit-2-not-4', 3, 4, 1, 1, 1, [(3, 4, 4)], 'the mask is 0x0002: a cell with only 0x04 is not counted')
    c9e('9E-04-bit-1-and-0', 3, 4, 1, 1, 1, [(3, 4, 3)], '0x03 has bit 1: counted')
    c9e('9E-05-ground-property-only', 3, 4, 1, 1, 1, [(3, 4, 0x100)], 'the high byte (GroundProperty) is masked out')
    c9e('9E-06-high-byte-bit1', 3, 4, 1, 1, 1, [(3, 4, 0x200)], 'bit 1 of the HIGH byte (0x0200) is not the mask 0x0002: not counted')
    c9e('9E-07-both-bytes', 3, 4, 1, 1, 1, [(3, 4, 0x102)], '0x102 = GroundProperty 1, Walkability 2: counted')
    c9e('9E-08-rect-count-equals-min', 10, 20, 3, 2, 3, [(10, 20, 2), (12, 20, 2), (11, 21, 2)], '3x2 rectangle with 3 walkable cells: count == min -> 1 (>=)')
    c9e('9E-09-rect-count-below-min', 10, 20, 3, 2, 4, [(10, 20, 2), (12, 20, 2), (11, 21, 2)], 'count 3 < 4')
    c9e('9E-10-advance-along-x', 5, 5, 3, 1, 2, [(5, 5, 2), (6, 5, 0), (7, 5, 2)], 'cells [2,0,2]: count 2 (a port that does not advance along x would count 3)')
    c9e('9E-11-advance-along-x-first-zero', 5, 5, 3, 1, 1, [(5, 5, 0), (6, 5, 2), (7, 5, 0)], 'cells [0,2,0]: count 1 (a no-advance port counts 0)')
    c9e('9E-12-row-stride', 5, 5, 1, 2, 2, [(5, 5, 2), (5, 6, 2), (5 + 52 - 52, 7, 2)], 'two rows of one column: the second row is y+1')
    c9e('9E-13-w-zero', 5, 5, 0, 3, 0, [(5, 5, 2)], 'w == 0: count 0, min 0 -> 1')
    c9e('9E-14-w-zero-min-1', 5, 5, 0, 3, 1, [(5, 5, 2)], 'w == 0: count 0 < 1 -> 0')
    c9e('9E-15-h-zero', 5, 5, 3, 0, 0, [(5, 5, 2)], 'h == 0: count 0, min 0 -> 1')
    c9e('9E-16-h-zero-min-1', 5, 5, 3, 0, 1, [(5, 5, 2)], 'h == 0: 0 < 1 -> 0')
    c9e('9E-17-min-zero-always-one', 0, 0, 1, 1, 0, [], 'min 0: always 1')
    c9e('9E-18-wrap-to-next-row', 51, 10, 2, 1, 2, [(51, 10, 2), (0, 11, 2)], 'x=51, w=2: the second cell is the linear next one = (0, y+1) (stride 52 for every x)')
    c9e('9E-19-wrap-not-same-row', 51, 10, 2, 1, 2, [(51, 10, 2), (0, 10, 2)], 'x=51, w=2: (0, y) is NOT the second cell')
    c9e('9E-20-last-cell', 51, 59, 1, 1, 1, [(51, 59, 2)], 'the last cell of the 52x60 grid')
    c9e('9E-21-full-width-row', 0, 30, 52, 1, 52, [(x, 30, 2) for x in range(52)], 'a full row of 52 walkable cells: count 52 >= 52')
    c9e('9E-22-full-width-row-minus-one', 0, 30, 52, 1, 52, [(x, 30, 2) for x in range(51)], '51 of 52: count < 52')
    c9e('9E-23-min-255', 0, 0, 1, 1, 255, [(0, 0, 2)], 'min 255 is unreachable for a 1x1')
    c9e('9E-24-x-y-order', 7, 2, 1, 1, 1, [(2, 7, 2)], 'bytes are x then y: (x=7, y=2) is not (x=2, y=7)')
    c9e('9E-25-w-h-order', 4, 4, 3, 1, 3, [(4, 4, 2), (5, 4, 2), (6, 4, 2)], 'bytes 3 then 4 are w then h: 3x1 is the row of 3')
    c9e('9E-26-stale-one-false', 3, 4, 1, 1, 1, [], 'false overwrites a stale 1', rb=1)
    c9e('9E-27-stale-zero-true', 3, 4, 1, 1, 1, [(3, 4, 2)], 'true overwrites a stale 0', rb=0)
    return out


# ------------------------------------------------------------------------------------------------------------------------------ sweeps and random
GROUP_TYPES = [0x80 + k for k in range(12)]
RAW_IDS = [0x01, 0x03, 0x18, 0x7F]   # raw id 0 is avoided on purpose: a wiped free slot has EntityRefId 0 in the binary (INFERENCE), the DLL has no free slot


def random_world(rng, nmax=8):
    n = rng.randint(2, nmax)
    slots = [hero(status=rng.choice([2, 2, 2, 3]), force_z=rng.choice([0, 1, -1, 5, -5, 0x10000, -0x10000, 0x7FFFFFFF, -0x80000000]),
                  target_anim=rng.choice([0, 1, 2, 9, 0x41, 0xFF, 0x141]))]
    for i in range(1, n):
        st = rng.choice([0, 1, 2, 2, 2, 3, 3, 4])
        if st == 0:
            for _ in range(10):
                rng.random()      # keep the draw count of a used slot (determinism does not depend on this, only on the seed)
            slots.append(free_slot())    # a free slot is WIPED (status 0, every field 0), as the binary leaves it
            continue
        slots.append(slot(status=st, ref_id=rng.choice([0, 1, 2, 3, 4, 5, 6, 7, 8, 0x14, 0x18]),
                          flags=rng.choice([0, 0x80, 0x80, 0x100, 0x180, 0x4100, 0x1]), anim_flags=rng.choice([0, 0, 0x80, 0x40, 0xC0]),
                          force_z=rng.choice([0, 0, 1, -1, 5, -5, 0x10000, -0x10000, 0x7FFFFFFF, -0x80000000]),
                          target_anim=rng.choice([0, 1, 2, 3, 9, 0x41, 0xFF, 0x141])))
    used = [i for i, s in enumerate(slots) if s['status'] != 0]
    for i, s in enumerate(slots):
        if s['status'] == 0:
            continue
        for k in ('parent', 'platform', 'riding', 'xcoll'):
            if rng.random() < 0.45:
                s[k] = rng.choice(used)
    return slots


def pick_ref(rng, slots):
    return rng.choice([i for i, s in enumerate(slots) if s['status'] != 0])


def sweep_vectors():
    out = []
    for op in (0xA9, 0xAA, 0xB4, 0xB5, 0xB6, 0xB7):
        for t in GROUP_TYPES + RAW_IDS:
            for rep in range(3):
                rng = random.Random(hashlib.sha256(('sweep-%02x-%02x-%d' % (op, t, rep)).encode()).digest())
                slots = random_world(rng)
                ref = pick_ref(rng, slots)
                instr = [op, t] + ([rng.choice([0, 1, 2, 9, 0x41, 0xFF])] if op == 0xB7 else [])
                out.append(V('SWEEP-%02X-t%02X-%d' % (op, t, rep), op, instr, ref, slots, note='search-type sweep (every group type 0..11 and sample raw ids), seeded random world',
                             result_before=RESULT_SENTINEL))
    for t1 in GROUP_TYPES + RAW_IDS:
        for t2 in (0x80, 0x81, 0x82, 0x83, 0x8B, 0x03):
            rng = random.Random(hashlib.sha256(('sweep-b2-%02x-%02x' % (t1, t2)).encode()).digest())
            slots = random_world(rng)
            out.append(V('SWEEP-B2-t%02X-t%02X' % (t1, t2), 0xB2, [0xB2, t1, t2], pick_ref(rng, slots), slots,
                         note='search-type sweep for the first group of B2', result_before=RESULT_SENTINEL))
    return out


def random_vectors(per_op=30):
    out = []
    for op in OPS:
        for k in range(per_op):
            rng = random.Random(hashlib.sha256(('rand-%02x-%d' % (op, k)).encode()).digest())
            if op == 0x9E:
                x, y = rng.randint(0, 51), rng.randint(0, 50)
                w, h = rng.randint(0, 6), rng.randint(0, 6)
                # keep the rectangle inside the 52 x 60 grid (the outside is not defined, see the README)
                while (y + h - 1) * GRID_W + (x + w - 1) >= GRID_W * GRID_H and (h > 0 and w > 0):
                    h -= 1
                out.append(V('RAND-9E-%02d' % k, 0x9E, [0x9E, x, y, w, h, rng.randint(0, 8)], 0, [hero()], grid_seed=rng.randint(0, 1 << 30),
                             note='seeded random grid and rectangle', result_before=RESULT_SENTINEL))
                continue
            slots = random_world(rng)
            ref = pick_ref(rng, slots)
            t = rng.choice(GROUP_TYPES + RAW_IDS + [rng.randint(1, 15)])
            instr = [op, t]
            if op == 0xB2:
                instr.append(rng.choice(GROUP_TYPES + RAW_IDS))
            if op == 0xB7:
                instr.append(rng.choice([0, 1, 2, 9, 0x41, 0xFF]))
            out.append(V('RAND-%02X-%02d' % (op, k), op, instr, ref, slots, note='seeded random world', result_before=RESULT_SENTINEL))
    return out


def main():
    outdir = sys.argv[1] if len(sys.argv) > 1 else os.path.join(HERE, 'out')
    os.makedirs(outdir, exist_ok=True)
    specs = hand_vectors() + sweep_vectors() + random_vectors()
    ids = [s['id'] for s in specs]
    assert len(ids) == len(set(ids)), 'duplicate vector ids'
    lines = []
    bad = []
    readmap = {}
    stats = {}
    for spec in specs:
        res = run_vector(spec)
        op = spec['op']
        assert spec['instr'][0] == op
        assert len(spec['instr']) == SIZES[op], (spec['id'], spec['instr'])
        rec = dict(id=spec['id'], op='0x%02X' % op, instr=['0x%02X' % b for b in spec['instr']], ref=spec['ref'], n_last=spec.get('n_last', len(spec['slots']) - 1),
                   result_before=spec.get('result_before', RESULT_SENTINEL), note=spec.get('note', ''), slots=spec['slots'])
        if 'cells' in spec:
            rec['cells'] = [list(c) for c in spec['cells']]
        if 'grid_seed' in spec:
            rec['grid_seed'] = spec['grid_seed']
        rec['expect'] = dict(v0=res['v0'], result=res['result'], writes_other=res['writes_other'])
        rec['red_today'] = dict(result=spec.get('result_before', RESULT_SENTINEL), code_index_advance=SIZES[op],
                                trace_kind='UnknownSkipped', fails_today=(res['result'] != spec.get('result_before', RESULT_SENTINEL)))
        rec['model_binary_agrees'] = res['model_binary_agrees']
        rec['dll_shape_agrees'] = res['dll_shape_agrees']
        if not res['dll_shape_agrees']:
            rec['dll_shape'] = res['dll_shape']
        rec['reads'] = sorted(res['reads'].keys())
        lines.append(json.dumps(rec, sort_keys=True))
        if res['unmapped'] or res['unmodeled'] or res['writes_other'] or not res['model_binary_agrees'] or res['v0'] != SIZES[op]:
            bad.append((spec['id'], res['unmapped'], res['unmodeled'], res['writes_other'], res['model_binary_agrees'], res['v0']))
        rm = readmap.setdefault('0x%02X' % op, {})
        for k, member in res['reads'].items():
            import re
            gk = re.sub(r'slot\[\d+\]', 'slot[i]', k)
            rm[gk] = member
        s = stats.setdefault('0x%02X' % op, dict(vectors=0, result0=0, result1=0, dll_shape_disagrees=0, green_today=0))
        s['vectors'] += 1
        s['result0'] += res['result'] == 0
        s['result1'] += res['result'] == 1
        s['dll_shape_disagrees'] += not res['dll_shape_agrees']
        s['green_today'] += not rec['red_today']['fails_today']
    with open(os.path.join(outdir, 'sm3-vectors.jsonl'), 'w', encoding='utf-8', newline='\n') as f:
        f.write('\n'.join(lines) + '\n')
    with open(os.path.join(outdir, 'read-map.json'), 'w', encoding='utf-8', newline='\n') as f:
        json.dump(readmap, f, indent=1, sort_keys=True)
        f.write('\n')
    with open(os.path.join(outdir, 'stats.json'), 'w', encoding='utf-8', newline='\n') as f:
        json.dump(stats, f, indent=1, sort_keys=True)
        f.write('\n')
    print('vectors:', len(specs))
    print(json.dumps(stats, indent=1, sort_keys=True))
    if bad:
        print('STOP: %d vector(s) with an unmapped read, an unmodeled write, a model disagreement or a wrong size:' % len(bad))
        for b in bad[:20]:
            print('  ', b)
        sys.exit(2)
    print('OK: every read mapped, no unmodeled write, binary == python model on every vector')


if __name__ == '__main__':
    main()
