"""E19.sm1 value oracle, frozen as vectors: the REAL handlers 0x8003F3F8 (0x60), 0x8003F488 (0x61), 0x8003E558 (0x3C), 0x8003E64C (0x3D) and the REAL
search 0x8003C954 of ALUN_CD.EXE (France) run in the minimal MIPS-I interpreter of the annex (emu.py, copied from docs/plan-e19-sm-annexe/sm1-sm4/work/oracle).
Only the id validator 0x8003C914 (its result is discarded; it is fatal for an invalid record id, never reached on valid data) and the debug printer 0x8003C840
(illegal search type, never used by a vector) are replaced by no-ops.

This module holds: the world schema, the writer of a world into the interpreter memory, the run with a trace of every DATA read, the classification of each
read into a named field (an unclassified read is a STOP), an independent python model of the four handlers (written for this slice from the listings
of sm1-sm4/check/listings, not copied), and the model of the DLL (EntitySearchService + the four proposed `case` blocks) with its mutants."""
import os
import struct
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import emu  # noqa: E402  (copy of the annex interpreter, same folder)

M32 = 0xFFFFFFFF
s32 = emu.s32

ENT0 = 0x80127D30
STRIDE = 0x294
NSLOTS_MEM = 10          # slots 0..9 are written in memory (slots beyond N hold junk)
NADDR = 0x80134360
RES = 0x8013D8D8         # first word of the results buffer (the handlers read 0x8013D8D4 + 4*i, i = count..1)
STATE = 0x8013D9E8
CODE = 0x80150000
PCP = 0x80150100
FOREIGN0 = 0x80192000    # addresses of nothing: a pointer field that holds one never equals a slot address
LO, HI = 0x80100000, 0x80190000
TABLE = 0x80098FAC
SWITCH = 0x80023C30      # the 12-entry jump table of the search (constant data of the image)

OFS = dict(parent=0x0C, status=0x10, blocked=0x20, platform=0x28, ref=0x48, flags=0x6C, anim=0xB4,
           tx=0x120, ty=0x124, tz=0x128, riding=0x12C, xcoll=0x130)
OFS_INV = {v: k for k, v in OFS.items()}

HANDLER = {0x3C: 0x8003E558, 0x3D: 0x8003E64C, 0x60: 0x8003F3F8, 0x61: 0x8003F488}
SIZE = {0x3C: 7, 0x3D: 7, 0x60: 2, 0x61: 2}

PATTERN = bytes(((x * 7 + 3) & 0xFF) for x in range(HI - LO))


def addr(i):
    return ENT0 + i * STRIDE


# ------------------------------------------------------------------------------------------------------------------------------ world schema
def S(status=2, ref=0, flags=0x80, anim=0, tile=(0, 0, 0), riding=None, xcoll=None, parent=None, platform=None, blocked=False):
    """One slot. status 0..4 (Destroyed, Loaded, Normal, Deactivated, FlagToDestroy). ref: the EntityRefId (-1 for the hero). flags/anim: raw words.
    tile: (TileX, TileY, TileZ) signed 32-bit. riding/xcoll/parent/platform: None, a slot index, or 'foreign' (a non-null pointer to nothing in the world).
    blocked: BlockedByEntity non-null (+0x20), a field none of the four handlers reads."""
    return dict(status=status, ref=ref, flags=flags, anim=anim, tile=list(tile), riding=riding, xcoll=xcoll, parent=parent, platform=platform, blocked=blocked)


def HERO(status=2, flags=0x3118C, anim=0, tile=(0, 0, 0), riding=None, **kw):
    """Slot 0, the hero: EntityRefId -1 (WorldProxy.cs:2111), IsPlayer, flags 0x3118C by default (A30)."""
    return S(status=status, ref=-1, flags=flags, anim=anim, tile=tile, riding=riding, **kw)


def ptr(v):
    if v is None:
        return 0
    if v == 'foreign':
        return FOREIGN0
    return addr(v)


def write_world(m, vec, rng_junk):
    """Writes the vector's world into the interpreter memory. Slots 0..n carry the vector's data; slots n+1..9 carry attractive junk
    (so a read beyond N would change a result); every unmapped word of every slot holds a recognisable pattern."""
    lo = LO & 0x1FFFFFFF
    m.ram[lo:lo + (HI - LO)] = PATTERN
    n = vec['n']
    m.ww(NADDR, n)
    for i in range(NSLOTS_MEM):
        for o in range(0, STRIDE, 4):
            m.ww(addr(i) + o, 0xA5000000 | (i << 16) | o)
        if i <= n:
            d = vec['slots'][i]
            tile = d['tile']
        else:
            # junk beyond N: active, matches raw ids 0..7, rides the logic entity, sits at the origin tile, collidable
            d = S(status=2, ref=rng_junk.randrange(0, 8), flags=0x80, anim=0, tile=(rng_junk.randrange(0, 3), rng_junk.randrange(0, 3), rng_junk.randrange(0, 3)),
                  riding=vec['logic'], xcoll=vec['logic'], parent=vec['logic'], platform=None)
            tile = d['tile']
        m.ww(addr(i) + OFS['status'], d['status'])
        m.ww(addr(i) + OFS['ref'], d['ref'] & M32)
        m.ww(addr(i) + OFS['flags'], d['flags'] & M32)
        m.ww(addr(i) + OFS['anim'], d['anim'] & M32)
        m.ww(addr(i) + OFS['tx'], tile[0] & M32)
        m.ww(addr(i) + OFS['ty'], tile[1] & M32)
        m.ww(addr(i) + OFS['tz'], tile[2] & M32)
        m.ww(addr(i) + OFS['riding'], ptr(d['riding']))
        m.ww(addr(i) + OFS['xcoll'], ptr(d['xcoll']))
        m.ww(addr(i) + OFS['parent'], ptr(d['parent']))
        m.ww(addr(i) + OFS['platform'], ptr(d['platform']))
        m.ww(addr(i) + OFS['blocked'], FOREIGN0 + 8 if d.get('blocked') else 0)
    for j in range(0x48):
        m.ww(RES - 4 + 4 * j, 0xDEADBEEF)
    for off in range(0, 0x40, 4):
        m.ww(STATE + off, 0xC0DE0000 | off)
    m.ww(STATE + 0x2C, 0x5A5A5A5A)
    for i, byte in enumerate(vec['code'] + [0] * 8):
        m.wb(CODE + i, byte)
    m.ww(PCP, CODE)


class TracedEmu(emu.Emu):
    """Interpreter that records every DATA read (instruction fetches are at pc and pc + 4 and are not recorded)."""

    def __init__(self):
        super().__init__()
        self.reads = []
        self.tracing = False

    def _note(self, a, size):
        if self.tracing:
            aa = a & 0xFFFFFFFF
            if aa not in (self.pc, self.pc + 4):
                self.reads.append((aa, size))

    def rb(self, a):
        self._note(a, 1)
        return super().rb(a)

    def rh(self, a):
        self._note(a, 2)
        return super().rh(a)

    def rw(self, a):
        self._note(a, 4)
        return super().rw(a)


def make_machine():
    m = TracedEmu()
    m.hooks[0x8003C914] = lambda mm: None
    m.hooks[0x8003C840] = lambda mm: None
    return m


class Stop(Exception):
    """An unclassified read or an unexpected write: the mapping is incomplete."""


def classify(vec, a, size):
    """Name of the world field a data read touches. Returns (kind, name) with kind in field, global, code, buffer, const, stack; raises Stop otherwise."""
    if ENT0 <= a < ENT0 + NSLOTS_MEM * STRIDE:
        i, off = divmod(a - ENT0, STRIDE)
        if off in OFS_INV and size == 4 and (a - ENT0) % 4 == 0:
            if i > vec['n']:
                raise Stop('read of slot %d beyond N=%d (%s)' % (i, vec['n'], OFS_INV[off]))
            return 'field', 'slot[%d].%s' % (i, OFS_INV[off])
        raise Stop('unmapped entity word: slot %d offset 0x%X size %d' % (i, off, size))
    if a == NADDR and size == 4:
        return 'global', 'N'
    if CODE <= a < CODE + 16 and size == 1:
        return 'code', 'code[%d]' % (a - CODE)
    if a == PCP and size == 4:
        return 'code', 'pc-pointer'
    if RES - 4 <= a < RES + 0x120 and size == 4:
        return 'buffer', 'buffer[%d]' % ((a - (RES - 4)) // 4)
    if SWITCH <= a < SWITCH + 0x30 and size == 4:
        return 'const', 'search-jump-table'
    if a >= 0x801FFE00:
        return 'stack', 'stack'
    raise Stop('unmapped read: %08X size %d' % (a, size))


def run_binary(m, vec, rng_junk):
    """Runs the real handler on the vector and returns the observed outcome. Raises Stop on an unmapped read or write."""
    import random  # noqa: F401
    write_world(m, vec, rng_junk)
    lo = LO & 0x1FFFFFFF
    before = bytes(m.ram[lo:lo + (HI - LO)])
    m.reads = []
    m.tracing = True
    logic = addr(vec['logic'])
    owner = addr(0)
    v0 = m.call(HANDLER[vec['op']], logic, owner, PCP, STATE)
    m.tracing = False
    after = bytes(m.ram[lo:lo + (HI - LO)])
    reads = set()
    buf_reads = []
    for (a, size) in m.reads:
        kind, name = classify(vec, a, size)
        if kind == 'field' or kind == 'global' or kind == 'code':
            reads.add(name)
        elif kind == 'buffer':
            buf_reads.append(name)
    # writes: everything that changed must be the Result word or the results buffer
    changed = []
    for off in range(0, HI - LO, 4):
        if before[off:off + 4] != after[off:off + 4]:
            changed.append(LO + off)
    other = []
    for a in changed:
        if a == STATE + 0x2C:
            continue
        if RES - 4 <= a < RES + 0x120:
            continue
        other.append(a)
    if other:
        raise Stop('write outside Result and the results buffer: %s' % ', '.join('%08X' % a for a in other[:6]))
    result = m.rw(STATE + 0x2C)
    # results buffer: the matches, in buffer order (slot indices)
    matches = []
    j = 0
    while True:
        w = m.rw(RES + 4 * j)
        if w == 0xDEADBEEF:
            break
        if not (ENT0 <= w < ENT0 + NSLOTS_MEM * STRIDE) or (w - ENT0) % STRIDE:
            raise Stop('results buffer holds a non-slot word %08X' % w)
        matches.append((w - ENT0) // STRIDE)
        j += 1
    return dict(v0=v0, result=result, reads=sorted(reads), matches=matches, buffer_reads=len(buf_reads))


# ------------------------------------------------------------------------------------------------------------------------------ my own binary model
def active(d):
    return 1 <= d['status'] <= 3          # (status - 1) < 3 unsigned: 0x8003E698 / the search


def eligible(d):
    return bool(d['flags'] & 0x80) and not (d['anim'] & 0x80) and d['platform'] is None


def model_search(vec, t):
    """0x8003C954 as listed in sm1-sm4/check/listings/search_full.txt: slot indices in buffer order."""
    n = vec['n']
    sl = vec['slots']
    ref = vec['logic']
    S_ = sl[ref]
    if (t & 0x80) == 0:
        return [i for i in range(1, n + 1) if sl[i]['ref'] == t] if active(S_) else []
    ft = t & 0x7F
    if ft == 0:
        return [ref]
    if ft == 1:
        return [0]
    if ft == 2:
        return [i for i in range(0, n + 1) if active(sl[i])]
    if ft == 3:
        return [i for i in range(1, n + 1) if active(sl[i])]
    if ft == 4:
        return [i for i in range(0, n + 1) if active(S_) and eligible(sl[i])]
    out = []
    for i in range(1, n + 1):
        c = sl[i]
        if not active(c):
            continue
        if ft == 5 and S_['riding'] == i:
            out.append(i)
        elif ft == 6 and c['riding'] == ref:
            out.append(i)
        elif ft == 7 and S_['xcoll'] == i:
            out.append(i)
        elif ft == 8 and c['xcoll'] == ref:
            out.append(i)
        elif ft == 9 and c['parent'] == ref:
            out.append(i)
        elif ft == 10 and S_['parent'] == i:
            out.append(i)
        elif ft == 11 and c['platform'] is not None:
            out.append(i)
    return out


def model_binary(vec):
    """Expected (v0, Result) of the handler, from my own reading of the listings."""
    op = vec['op']
    b = vec['code']
    n = vec['n']
    sl = vec['slots']
    if op in (0x3C, 0x3D):
        for i in range(0, n + 1):
            c = sl[i]
            if not active(c):
                continue
            if op == 0x3C and not eligible(c):
                continue
            tx, ty, tz = c['tile']
            if b[1] <= tx <= b[2] and b[3] <= ty <= b[4] and b[5] <= tz <= b[6]:
                return 7, 1
        return 7, 0
    group = model_search(vec, b[1])
    L = vec['logic']
    if op == 0x60:
        return 2, int(any(sl[i]['riding'] == L for i in group))
    if op == 0x61:
        return 2, int(sl[L]['riding'] is not None and any(sl[L]['riding'] == i for i in group))
    raise ValueError(op)


# ------------------------------------------------------------------------------------------------------------------------------ the DLL model
class P:
    """A proxy of the montage. identity = ('E', k) for slot k: its own LogicContextEntity; 'anon' = a bare proxy without LogicContextEntity."""
    __slots__ = ('k', 'status', 'ref', 'flags', 'anim', 'tile', 'riding', 'xcoll', 'parent', 'platform', 'blocked', 'is_player', 'identity')

    def __init__(self, k, d, anon=False):
        self.k = k
        self.status = d['status']
        self.ref = d['ref']
        self.flags = d['flags'] & M32
        self.anim = d['anim'] & M32
        self.tile = tuple(d['tile'])
        self.riding = d['riding']        # None | slot index | 'foreign'  (an Entity?)
        self.xcoll = d['xcoll']          # None | slot index (a proxy)
        self.parent = d['parent']
        self.platform = d['platform']
        self.blocked = d.get('blocked', False)
        self.is_player = (k == 0)
        self.identity = None if anon else ('E', k)

    @property
    def loaded_normal_or_deactivated(self):
        return self.status in (1, 2, 3)

    @property
    def is_active(self):                    # EntityStatusExtensions.IsActive: Normal or Deactivated
        return self.status in (2, 3)

    @property
    def eligible_subject(self):             # EntitySupport.IsEligibleSubject
        return bool(self.flags & 0x80) and not (self.anim & 0x80) and self.platform is None


def ident(ptr_value):
    """The Entity? a pointer field holds: ('E', k) for a slot, ('F', 0) for a foreign one, None."""
    if ptr_value is None:
        return None
    if ptr_value == 'foreign':
        return ('F', 0)
    return ('E', ptr_value)


class Montage:
    """The DLL's world of a vector. hero_in_list: SpawnedEntities holds the hero at index 0 (production, WorldProxy.cs:2304); otherwise only PlayerEntity does
    (the bare Context of AlundraClassAndBranchOpcodesTests). Every proxy has a non-null LogicContextEntity unless listed in anon."""

    def __init__(self, vec, hero_in_list=True, anon=()):
        self.vec = vec
        self.proxies = [P(k, d, anon=(k in anon)) for k, d in enumerate(vec['slots'])]
        self.player = self.proxies[0]
        self.spawned = list(self.proxies) if hero_in_list else list(self.proxies[1:])
        self.logic = self.proxies[vec['logic']]


def dll_search(mt, t):
    """EntitySearchService.GetMatchingEntitiesBySearchType, from Alundra/Entities/EntitySearchService.cs:99-269 (HEAD); list of proxies in order."""
    ref = mt.logic
    out = []
    if (t & 0x80) == 0:
        if ref.loaded_normal_or_deactivated:
            for c in mt.spawned:
                if c.ref == t:
                    out.append(c)
        return out
    ft = t & 0x7F
    if ft == 0:
        return [ref]
    if ft == 1:
        return [mt.player] if mt.player is not None else []
    if ft == 2:
        return [c for c in mt.spawned if c.loaded_normal_or_deactivated]
    if ft == 3:
        return [c for c in mt.spawned if c.loaded_normal_or_deactivated and not c.is_player]
    if ft == 4:
        return [c for c in mt.spawned if ref.loaded_normal_or_deactivated and bool(c.flags & 0x80) and not (c.anim & 0x80) and c.platform is None]
    for c in mt.spawned:
        if c.is_player or not c.loaded_normal_or_deactivated:
            continue
        if ft == 5 and same(ident(ref.riding), c.identity):
            out.append(c)
        elif ft == 6 and same(ident(c.riding), ref.identity):
            out.append(c)
        elif ft == 7 and ref.xcoll is not None and ref.xcoll == c.k:
            out.append(c)
        elif ft == 8 and c.xcoll is not None and c.xcoll == ref.k:
            out.append(c)
        elif ft == 9 and same(ident(c.parent), ref.identity):
            out.append(c)
        elif ft == 10 and same(ident(ref.parent), c.identity):
            out.append(c)
        elif ft == 11 and c.platform is not None:
            out.append(c)
    return out


def same(a, b):
    """object.ReferenceEquals on two Entity? references: null == null is true."""
    return a is b if (a is None or b is None) else a == b


# ---- the four proposed `case` blocks (rules SM1-R1 to SM1-R4), and their mutants -------------------------------------------------
# A mutant is a keyword set; the reference behaviour is `mutant=None`.
def dll_case(mt, op, code, mutant=None):
    """Returns (size, Result or None when the case leaves Result untouched)."""
    v = code
    logic = mt.logic
    owner = mt.proxies[0]
    if op in (0x60, 0x61):
        if mutant == 'swap_60_61':
            op = 0x61 if op == 0x60 else 0x60
        group = dll_search(mt, v[1]) if mutant != 'no_player_in_search' else dll_search_no_player(mt, v[1])
        if mutant == 'first_only':
            group = group[:1]
        if mutant == 'last_only':
            group = group[-1:]
        ref_logic = owner if mutant == 'owner_instead_of_logic' else logic
        if op == 0x60:
            def rides(c):
                if mutant == 'reuse_IsRidingTheLogicEntity':
                    return c.is_active and not c.blocked and ident(c.riding) is not None and same(ident(c.riding), ref_logic.identity)
                if mutant == 'no_null_guard':
                    return same(ident(c.riding), ref_logic.identity)          # null == null is true for a bare logic entity
                if mutant == 'platform_instead_of_riding':
                    return same(ident(c.platform), ref_logic.identity) and c.platform is not None
                return ident(c.riding) is not None and same(ident(c.riding), ref_logic.identity)
            res = int(any(rides(c) for c in group))
        else:
            def carried_by(c):
                if mutant == 'no_null_guard':
                    return same(ident(ref_logic.riding), c.identity)           # null == null is true for a candidate without identity
                return ident(ref_logic.riding) is not None and same(ident(ref_logic.riding), c.identity)
            res = int(any(carried_by(c) for c in group))
        if mutant == 'empty_group_keeps_result' and not group:
            return SIZE[op], None
        if mutant == 'empty_group_one' and not group:
            res = 1
        if mutant == 'result_always_one':
            res = 1
        size = SIZE[op]
        if mutant == 'size_one':
            size = 1
        if mutant == 'size_three':
            size = 3
        return size, res
    # 0x3C / 0x3D
    on_ground = (op == 0x3C)
    if mutant == 'zone_3c_equals_3d':
        on_ground = False
    if mutant == 'zone_3d_equals_3c':
        on_ground = True
    b = list(v)
    if mutant == 'signed_operands':
        b = [x - 256 if x >= 128 else x for x in b]
    cands = []
    if mutant != 'spawned_only':
        cands.append(mt.player)
    cands.extend(mt.spawned)
    if mutant == 'skip_last':
        cands = cands[:-1]
    if mutant == 'logic_only':
        cands = [logic]
    seen_any = False

    def status_ok(c):
        if mutant == 'no_status_filter':
            return True
        if mutant == 'active_only':
            return c.is_active
        return c.loaded_normal_or_deactivated

    def flags_ok(c):
        if not on_ground:
            return True
        if mutant == 'inverted_3c':
            return (not (c.flags & 0x80)) and bool(c.anim & 0x80) and c.platform is None
        if mutant == '3c_no_collidable':
            return not (c.anim & 0x80) and c.platform is None
        if mutant == '3c_no_anim':
            return bool(c.flags & 0x80) and c.platform is None
        if mutant == '3c_no_platform':
            return bool(c.flags & 0x80) and not (c.anim & 0x80)
        if mutant == '3c_flags_equal':
            return c.flags == 0x80 and not (c.anim & 0x80) and c.platform is None
        if mutant == '3c_anim_nonzero':
            return bool(c.flags & 0x80) and c.anim == 0 and c.platform is None
        return c.eligible_subject

    def inside(c):
        tx, ty, tz = c.tile
        if mutant == 'axes_xy_swapped':
            tx, ty = ty, tx
        if mutant == 'axes_yz_swapped':
            ty, tz = tz, ty
        lo_x, hi_x, lo_y, hi_y, lo_z, hi_z = b[1], b[2], b[3], b[4], b[5], b[6]
        if mutant == 'upper_exclusive':
            return lo_x <= tx < hi_x and lo_y <= ty < hi_y and lo_z <= tz < hi_z
        if mutant == 'lower_exclusive':
            return lo_x < tx <= hi_x and lo_y < ty <= hi_y and lo_z < tz <= hi_z
        if mutant == 'z_ignored':
            return lo_x <= tx <= hi_x and lo_y <= ty <= hi_y
        if mutant == 'byte_truncated_tile':
            tx, ty, tz = tx & 0xFF, ty & 0xFF, tz & 0xFF
        if mutant == 'unsigned_tile':
            tx, ty, tz = tx & M32, ty & M32, tz & M32
        return lo_x <= tx <= hi_x and lo_y <= ty <= hi_y and lo_z <= tz <= hi_z

    res = 0
    for c in cands:
        if c is None:
            continue
        if status_ok(c) and flags_ok(c) and inside(c):
            res = 1
            break
    size = 7
    if mutant == 'size_six':
        size = 6
    if mutant == 'size_eight':
        size = 8
    if mutant == 'result_only_on_true' and res == 0:
        return size, None
    return size, res


def dll_search_no_player(mt, t):
    """EntitySearchService called without the playerEntity argument (type 1 then finds nobody)."""
    saved = mt.player
    mt.player = None
    try:
        return dll_search(mt, t)
    finally:
        mt.player = saved


MUTANTS_60_61 = ['swap_60_61', 'no_player_in_search', 'first_only', 'last_only', 'owner_instead_of_logic', 'reuse_IsRidingTheLogicEntity', 'no_null_guard',
                 'platform_instead_of_riding', 'empty_group_keeps_result', 'empty_group_one', 'result_always_one', 'size_one', 'size_three']
MUTANTS_3C_3D = ['zone_3c_equals_3d', 'zone_3d_equals_3c', 'signed_operands', 'spawned_only', 'skip_last', 'logic_only', 'no_status_filter', 'active_only',
                 'inverted_3c', '3c_no_collidable', '3c_no_anim', '3c_no_platform', '3c_flags_equal', '3c_anim_nonzero', 'axes_xy_swapped', 'axes_yz_swapped',
                 'upper_exclusive', 'lower_exclusive', 'z_ignored', 'byte_truncated_tile', 'unsigned_tile', 'size_six', 'size_eight', 'result_only_on_true']
