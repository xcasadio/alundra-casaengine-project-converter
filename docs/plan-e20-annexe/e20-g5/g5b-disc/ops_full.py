"""ops_full.py - the three handlers 0x72 (0x8003FA24), 0xA9 (0x80041174) and 0xAA (0x80041200) of ALUN_CD.EXE (France) against a Python model written from the
disassembly, on the REAL code (the minimal MIPS interpreter of work/emu), plus one-word mutants of the real handlers (in the interpreter's RAM only) killed by the
same vectors, plus goldens.

Layers
  A. handler level, ALL 256 operand values: the real search helper 0x8003C954 is run first to read the entity buffer it fills (count + addresses); the model of the
     handler takes that buffer; the snapshot of the whole observable memory (10 entity slots, the program state, the code cell, the code bytes, the count word) must
     equal the model's, the return value (instruction length) too.
  B. helper level, for the search types the sites use or that matter for XCollisionEntity: record id (low), 0x80, 0x81, 0x82, 0x83, 0x87, 0x88 (and the illegal ids
     0x8C-0xFF): the model predicts count and buffer order; compared with the real helper.
Stubs journalled: CheckEntityRecord 0x8003C914 ('check', id) and the error logger 0x8003C840 ('err').
Run: python -B -X utf8 ops_full.py [N] [mut] [gold]"""
import os, random, struct, sys, json, hashlib
HERE = os.path.dirname(os.path.abspath(__file__))
WORK = os.path.join(HERE, 'work')
sys.path.insert(0, WORK)
sys.path.insert(0, os.path.join(WORK, 'emu'))
import capstone
from h import World, Ent, ENT0, STRIDE, M32, s32
import emu as _emu

H72, H72_END = 0x8003FA24, 0x8003FA3C
HA9, HA9_END = 0x80041174, 0x80041200
HAA, HAA_END = 0x80041200, 0x80041290
HLP, HLP_END = 0x8003C954, 0x8003CF20
NUM = 0x80134360          # g_numberOfEntities
BUF = 0x8013D8D8          # g_matchingEntitiesBuffer
STATE = 0x80141000        # a program state; Result at +0x2C
CELL = 0x80141100         # the cell the handler's a2 points at; it holds the code pointer
CODE = 0x80141200         # the instruction bytes
NSLOT = 10
ST_STATE, ST_ID, XCOLL = 0x10, 0x48, 0x130
TANIM, TDIR, LANIM, LDIR = 0x88, 0x8C, 0x26C, 0x270
ARGS = [a for a in sys.argv[1:]]
N = next((int(a) for a in ARGS if a.isdigit()), 6000)
DO_MUT = 'mut' in ARGS
DO_GOLD = 'gold' in ARGS

W = World()
M = W.m
J = []
M.hooks[0x8003C914] = lambda m: J.append(('check', m.r[4] & M32))
M.hooks[0x8003C840] = lambda m: J.append(('err',))
COV = set()
_o = _emu.Emu.exec1


def _x(self, pc, wd):
    COV.add(pc)
    return _o(self, pc, wd)


_emu.Emu.exec1 = _x
rnd = random.Random(20261023)


def addr(slot):
    return ENT0 + slot * STRIDE


class Spec:
    """one world: NSLOT entity images, g_numberOfEntities, the owner slot, the operand"""

    def __init__(self):
        self.ents = [Ent(bytes(rnd.randrange(256) for _ in range(STRIDE))) for _ in range(NSLOT)]
        for e in self.ents:
            e.set32(ST_STATE, rnd.choice([0, 1, 1, 2, 3, 3, 4, 5, rnd.randrange(1 << 32)]))
            e.set32(ST_ID, rnd.choice([0, 1, 2, 5, 24, 24, 25, 127, rnd.randrange(1 << 32)]))
            e.set32(XCOLL, self.ptr())
        self.num = rnd.choice([0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 9, 9, 10, rnd.randrange(-3, 12) & M32])
        self.owner = rnd.choice([0, 1, 2, 3, 4, 5, 6, 7, 8, 9])
        r = rnd.random()
        if r < 0.35:
            self.op = rnd.choice([0x81, 0x81, 0x81, 24, 0x80, 0x82, 0x83, 0x87, 0x88])
        elif r < 0.55:
            self.op = rnd.choice([0, 1, 2, 5, 24, 25, 127])
        else:
            self.op = rnd.randrange(256)
        self.result0 = rnd.choice([0, 1, 2, rnd.randrange(1 << 32)])
        self.a1 = rnd.randrange(1 << 32)
        # make the interesting predicates true often
        o = self.ents[self.owner]
        if rnd.random() < 0.4:
            self.ents[0].set32(XCOLL, addr(self.owner))      # the hero is blocked by the owner (0xAA [129])
        if rnd.random() < 0.4:
            o.set32(XCOLL, addr(0))                          # the owner is blocked by the hero (0xA9 [129])
        if rnd.random() < 0.15:
            o.set32(XCOLL, addr(rnd.randrange(NSLOT)))
        if (self.op & 0x80) == 0 and rnd.random() < 0.6:     # a record id search that finds something
            picks = [rnd.randrange(1, NSLOT) for _ in range(rnd.choice([1, 1, 2, 3]))]
            for k in picks:
                self.ents[k].set32(ST_ID, self.op)
            if rnd.random() < 0.5:
                o.set32(XCOLL, addr(rnd.choice(picks)))
            if rnd.random() < 0.5:
                self.ents[rnd.choice(picks)].set32(XCOLL, addr(self.owner))
        if (self.op & 0x80) and (self.op & 0x7F) in (2, 3, 7, 8, 5, 6, 9, 10, 11) and rnd.random() < 0.5:   # states loaded, collisions aimed at the owner
            for k in range(NSLOT):
                self.ents[k].set32(ST_STATE, rnd.choice([1, 2, 3]))
            if rnd.random() < 0.6:
                self.ents[rnd.randrange(NSLOT)].set32(XCOLL, addr(self.owner))
            if rnd.random() < 0.6:
                o.set32(XCOLL, addr(rnd.randrange(NSLOT)))

    def ptr(self):
        return rnd.choice([0, 0, addr(rnd.randrange(NSLOT)), addr(rnd.randrange(NSLOT)), addr(rnd.randrange(NSLOT)) + rnd.choice([1, 4, 0x10]),
                           rnd.randrange(1 << 32)])


def load(sp, op, operand=None):
    """put the world in the machine (all regions overwritten, buffer filled with a sentinel pattern)"""
    for k in range(NSLOT):
        W.put(k, sp.ents[k])
    for k in range(NSLOT, NSLOT + 4):
        W.put(k, Ent())                                      # the slots past the world are zero memory
    M.ww(NUM, sp.num)
    for i in range(0x20):
        M.ww(BUF + 4 * i, 0xDEAD0000 + i)
    for i in range(0x40 // 4):
        M.ww(STATE + 4 * i, 0xA5A50000 + i)
    M.ww(STATE + 0x2C, sp.result0)
    M.ww(CELL, CODE)
    M.wb(CODE, op)
    M.wb(CODE + 1, sp.op if operand is None else operand)
    M.wb(CODE + 2, 0xEE)
    J.clear()


REGIONS = [(ENT0, NSLOT * STRIDE), (STATE, 0x40), (CELL, 4), (CODE, 4), (NUM, 4)]


def snap(with_buf=False):
    parts = [bytes(M.ram[a & 0x1FFFFFFF:(a & 0x1FFFFFFF) + n]) for a, n in REGIONS]
    if with_buf:
        parts.append(bytes(M.ram[BUF & 0x1FFFFFFF:(BUF & 0x1FFFFFFF) + 0x80]))
    return b''.join(parts)


def run_handler(h, sp, op):
    load(sp, op)
    v0 = M.call(h, addr(sp.owner), sp.a1, CELL, STATE)
    return v0


def run_helper(sp, owner, typ):
    load(sp, 0)
    cnt = M.call(HLP, addr(owner), typ)
    return cnt, [M.rw(BUF + 4 * i) for i in range(max(0, min(cnt, 0x10)))]


# ---------------------------------------------------------------------------------------------------------------------------------------------
# the model: written from the disassembly (docs in the report), reads the buffer the helper produced
# ---------------------------------------------------------------------------------------------------------------------------------------------
def patch(blob, off, val):
    b = bytearray(blob)
    b[off:off + 4] = (val & M32).to_bytes(4, 'little')
    return bytes(b)


ENT_OFF = 0
STATE_OFF = NSLOT * STRIDE


def ent_off(a):
    """offset in the blob of the entity at address a, or None when a is not a slot address of the world"""
    d = a - ENT0
    if d < 0 or d % STRIDE or d // STRIDE >= NSLOT:
        return None
    return (d // STRIDE) * STRIDE


def rd(blob, a, o):
    return int.from_bytes(blob[ent_off(a) + o:ent_off(a) + o + 4], 'little')


def model_handler(op, blob, owner, buf):
    """op in (0x72, 0xA9, 0xAA); blob = snapshot before (without the buffer); buf = [addresses] filled by the helper (index order)
    returns (blob_after, v0)"""
    oa = addr(owner)
    oo = ent_off(oa)
    if op == 0x72:
        b = blob
        b = patch(b, oo + LANIM, int.from_bytes(blob[oo + TANIM:oo + TANIM + 4], 'little'))
        b = patch(b, oo + LDIR, int.from_bytes(blob[oo + TDIR:oo + TDIR + 4], 'little'))
        return b, 1
    res = 0
    for m in reversed(buf):             # the binary walks the buffer backward; any match gives 1
        if op == 0xA9:
            if rd(blob, oa, XCOLL) == m:
                res = 1
                break
        else:
            eo = ent_off(m)
            if eo is not None and int.from_bytes(blob[eo + XCOLL:eo + XCOLL + 4], 'little') == oa:
                res = 1
                break
    return patch(blob, STATE_OFF + 0x2C, res), 2


def model_search(sp, owner, typ):
    """the helper for the modelled types; returns (count, [addresses]) or None when not modelled"""
    def loaded(k):
        return k < NSLOT and ((sp.ents[k].u32(ST_STATE) - 1) & M32) < 3

    def ref_id(k):
        return sp.ents[k].u32(ST_ID) if k < NSLOT else 0      # a slot past the world is zero memory
    n = s32(sp.num)
    out = []
    if (typ & 0x80) == 0:
        # the OWNER's state is tested, not the entity's (0x8003C9AC: lw v0,0x10($s3)); slots 1..n inclusive; slots past NSLOT read as zero memory
        if loaded(owner):
            for i in range(1, n + 1):
                if ref_id(i) == typ:
                    out.append(addr(i))
        return len(out), out
    fn = typ & 0x7F
    if fn == 0:
        return 1, [addr(owner)]
    if fn == 1:
        return 1, [ENT0]
    if fn == 2 or fn == 3:
        lo = 0 if fn == 2 else 1
        if fn == 2 and n < 0:
            return 0, []
        for i in range(lo, n + 1):          # INCLUSIVE of n (the decompilation has i < n)
            if i < NSLOT and loaded(i):
                out.append(addr(i))
        return len(out), out
    if fn == 7 or fn == 8:
        for i in range(1, n + 1):
            if i < NSLOT and loaded(i):
                if fn == 7 and sp.ents[owner].u32(XCOLL) == addr(i):
                    out.append(addr(i))
                if fn == 8 and sp.ents[i].u32(XCOLL) == addr(owner):
                    out.append(addr(i))
        return len(out), out
    if fn >= 12:
        return 0, []                       # the error branch: journal ('err',), count 0
    return None


# ---------------------------------------------------------------------------------------------------------------------------------------------
# layer A + B
# ---------------------------------------------------------------------------------------------------------------------------------------------

SENT = _emu.SENTINEL
CALLEE = list(range(16, 24)) + [28, 29, 30]      # s0-s7, gp, sp, fp


def call_strict(h, a0, a1, a2, a3):
    """M.call, but the callee-saved registers (s0-s7, gp, sp, fp) and the return address are SET to known values first and CHECKED after the return:
    a handler whose frame code is mutated (sp adjust, save or restore offsets) is then killed. Returns (v0, frame_ok)."""
    saved = list(M.r)
    r = M.r
    known = {i: (0x51000000 + i * 0x111) & M32 for i in CALLEE if i != 29}
    for i, v in known.items():
        r[i] = v
    r[4], r[5], r[6], r[7] = a0 & M32, a1 & M32, a2 & M32, a3 & M32
    r[29] = 0x801FFF00
    r[31] = SENT
    for k in range(0x40):                      # a fresh stack: a restore from a slot the frame never wrote reads zero, not the previous call's value
        M.wb(0x801FFF00 - 0x40 + k, 0)
    M.pc = h
    n = 0
    while M.pc != SENT:
        if M.pc in M.hooks:
            M.hooks[M.pc](M)
            M.pc = r[31]
            continue
        M.step()
        n += 1
        if n > 100000:
            raise RuntimeError('step limit')
    ok = all(r[i] == v for i, v in known.items()) and r[29] == 0x801FFF00
    v0 = r[2]
    M.r = saved
    return v0, ok


def check_vector(sp, op, mutated=False):
    """returns None when the real handler equals the model, else a description"""
    if op == 0x72:
        h = H72
        buf = []
    else:
        h = HA9 if op == 0xA9 else HAA
        # the helper's real answer, for the model
        cnt, buf = run_helper_clean(sp, sp.owner, sp.op)
    load(sp, op)
    pre = snap()
    v0, fok = call_strict(h, addr(sp.owner), sp.a1, CELL, STATE)
    post = snap()
    mb, mv = model_handler(op, pre, sp.owner, buf)
    if post != mb or (v0 & M32) != mv or not fok:
        return ('handler', op, sp.op, v0, mv, 'frame_ok=%s' % fok)
    return None


def run_helper_clean(sp, owner, typ):
    # the helper is never mutated: a clean copy of its words is not needed because mutants only touch handler words
    load(sp, 0, typ)
    cnt = M.call(HLP, addr(owner), typ)
    return cnt, [M.rw(BUF + 4 * i) for i in range(max(0, min(cnt, 0x10)))]


def build(n):
    vs = []
    for k in range(n):
        vs.append((Spec(), (0x72, 0xA9, 0xAA)[k % 3]))
    return vs


vectors = build(N)
mism = 0
first = None
for sp, op in vectors:
    r = check_vector(sp, op)
    if r:
        mism += 1
        first = first or r

# layer B
bad_b = 0
nb = 0
typs_seen = {}
first_b = None
for sp, op in vectors[:N]:
    typ = sp.op
    ms = model_search(sp, sp.owner, typ)
    cnt, buf = run_helper_clean(sp, sp.owner, typ)
    jr = list(J)
    typs_seen[typ] = typs_seen.get(typ, 0) + 1
    if ms is None:
        continue
    nb += 1
    exp_j = [('check', typ)] if (typ & 0x80) == 0 else ([('err',)] if (typ & 0x7F) >= 12 else [])
    if (cnt & M32) != ms[0] or buf != ms[1][:0x10] or jr != exp_j:
        bad_b += 1
        first_b = first_b or (typ, sp.num, sp.owner, cnt, buf, ms, jr, exp_j)

# the decompilation's literal bound for the search types 2 and 3 (GameEngine.cs:1965-1988: i < g_numberOfEntities) against the real helper
dec_bad = dec_n = 0
for sp, op in vectors:
    typ = sp.op
    if (typ & 0x80) and (typ & 0x7F) in (2, 3):
        dec_n += 1
        n = s32(sp.num)
        lo = 0 if (typ & 0x7F) == 2 else 1
        dec = [addr(i) for i in range(lo, n) if i < NSLOT and ((sp.ents[i].u32(ST_STATE) - 1) & M32) < 3]
        cnt, buf = run_helper_clean(sp, sp.owner, typ)
        if dec != buf[:len(dec)] or cnt != len(dec):
            dec_bad += 1
print('decompilation literal bound (i < n) for the types 0x82 and 0x83: %d vectors, %d differ from the real helper (which loops 0..n / 1..n INCLUSIVE)' % (dec_n, dec_bad))

cov_all = set(COV)


def span(lo, hi):
    return [p for p in range(lo, hi, 4)]


miss = {}
for nm, lo, hi in (('0x72', H72, H72_END), ('0xA9', HA9, HA9_END), ('0xAA', HAA, HAA_END), ('helper', HLP, HLP_END)):
    ms_ = [p for p in span(lo, hi) if p not in cov_all]
    miss[nm] = ms_
    print('%-6s 0x%08X..0x%08X %3d words, executed %3d, never executed %s' % (nm, lo, hi, (hi - lo) // 4, (hi - lo) // 4 - len(ms_), ' '.join('%08x' % p for p in ms_) or '-'))
print('layer A (handlers, %d vectors, 256 operands drawn): %d mismatches' % (N, mism))
if first:
    print('  first:', first)
print('layer B (helper, %d vectors of modelled types): %d mismatches' % (nb, bad_b))
if first_b:
    print('  first:', first_b)
print('operand coverage: %d distinct values of the 256 drawn' % len(typs_seen))

# outcome classes of the model
cls = {}
for sp, op in vectors:
    if op == 0x72:
        key = ('0x72',)
    else:
        cnt, buf = run_helper_clean(sp, sp.owner, sp.op)
        load(sp, op)
        pre = snap()
        mb, mv = model_handler(op, pre, sp.owner, buf)
        res = int.from_bytes(mb[STATE_OFF + 0x2C:STATE_OFF + 0x30], 'little')
        kind = 'player' if sp.op == 0x81 else ('record' if not sp.op & 0x80 else 'fn%02X' % (sp.op & 0x7F) if (sp.op & 0x7F) < 12 else 'illegal')
        key = ('0x%02X' % op, kind, 'buf=%s' % ('empty' if not buf else ('1' if len(buf) == 1 else 'many')), 'Result=%d' % res)
    cls[key] = cls.get(key, 0) + 1
print('model outcome classes: %d' % len(cls))
for k, v in sorted(cls.items()):
    print('  ', k, v)

# ---------------------------------------------------------------------------------------------------------------------------------------------
# mutants of the three handlers
# ---------------------------------------------------------------------------------------------------------------------------------------------
if DO_MUT:
    md = capstone.Cs(capstone.CS_ARCH_MIPS, capstone.CS_MODE_MIPS32 + capstone.CS_MODE_LITTLE_ENDIAN)

    def dis(a, w):
        for i in md.disasm(struct.pack('<I', w), a):
            return '%s %s' % (i.mnemonic, i.op_str)
        return '.word %08x' % w

    def mutants(w):
        op = w >> 26
        imm = w & 0xFFFF
        out = []
        if w == 0:
            return out
        if op == 4: out.append(('beq->bne', (5 << 26) | (w & 0x3FFFFFF)))
        elif op == 5: out.append(('bne->beq', (4 << 26) | (w & 0x3FFFFFF)))
        elif op == 6: out.append(('blez->bgtz', (7 << 26) | (w & 0x3FFFFFF)))
        elif op == 7: out.append(('bgtz->blez', (6 << 26) | (w & 0x3FFFFFF)))
        elif op == 1: out.append(('regimm flip', w ^ (1 << 16)))
        elif op in (0xA, 0xB, 8, 9):
            out.append(('imm+1', (w & ~0xFFFF) | ((imm + 1) & 0xFFFF)))
            out.append(('imm-1', (w & ~0xFFFF) | ((imm - 1) & 0xFFFF)))
        elif op == 0xC:
            out.append(('andi mask<<1', (w & ~0xFFFF) | ((imm << 1) & 0xFFFF)))
        elif op == 0xD:
            out.append(('ori imm^lowbit', (w & ~0xFFFF) | (imm ^ (imm & -imm if imm else 1))))
        elif op == 0xF:
            out.append(('lui+1', (w & ~0xFFFF) | ((imm + 1) & 0xFFFF)))
        elif op in (0x20, 0x21, 0x23, 0x24, 0x25, 0x28, 0x29, 0x2B):
            out.append(('off+4', (w & ~0xFFFF) | ((imm + 4) & 0xFFFF)))
        elif op == 0 and (w & 63) in (0x2A, 0x2B):
            out.append(('slt<->sltu', w ^ 1))
        elif op == 0 and (w & 63) in (0x21, 0x23):
            out.append(('addu<->subu', w ^ 2))
        elif op == 0 and (w & 63) in (0x24, 0x25):
            out.append(('and<->or', w ^ 1))
        elif op == 3 or op == 2:
            out.append(('j/jal target+4', (w & ~0x3FFFFFF) | (((w & 0x3FFFFFF) + 1) & 0x3FFFFFF)))
        return out

    # per-handler vector subsets (drawn from the same stream)
    subs = {0x72: [v for v in vectors if v[1] == 0x72][:1200], 0xA9: [v for v in vectors if v[1] == 0xA9][:1500], 0xAA: [v for v in vectors if v[1] == 0xAA][:1500]}
    # pre-compute the helper buffers once (the helper is not mutated)
    pre = {}
    for op, vs in subs.items():
        L = []
        for sp, o in vs:
            buf = run_helper_clean(sp, sp.owner, sp.op)[1] if op != 0x72 else []
            L.append((sp, buf))
        pre[op] = L

    def dead(op, h):
        for sp, buf in pre[op]:
            load(sp, op)
            before = snap()
            try:
                v0, fok = call_strict(h, addr(sp.owner), sp.a1, CELL, STATE)
            except Exception:
                return True
            mb, mv = model_handler(op, before, sp.owner, buf)
            if snap() != mb or (v0 & M32) != mv or not fok:
                return True
        return False

    total = killed = 0
    surv = []
    for op, lo, hi in ((0x72, H72, H72_END), (0xA9, HA9, HA9_END), (0xAA, HAA, HAA_END)):
        for a in range(lo, hi, 4):
            w = M.rw(a)
            for name, nw in mutants(w):
                total += 1
                M.ww(a, nw)
                try:
                    d = dead(op, lo)
                except Exception:
                    d = True
                M.ww(a, w)
                if d:
                    killed += 1
                else:
                    surv.append((op, a, name, dis(a, w), dis(a, nw)))
    print('mutants %d killed %d survivors %d' % (total, killed, len(surv)))
    for s in surv:
        print('  SURVIVOR 0x%02X %08x %-16s %-30s -> %s' % s)

# ---------------------------------------------------------------------------------------------------------------------------------------------
# goldens: one vector per outcome class
# ---------------------------------------------------------------------------------------------------------------------------------------------
if DO_GOLD:
    seen = {}
    gold = []
    for sp, op in vectors:
        if op == 0x72:
            key = ('0x72',)
        else:
            cnt, buf = run_helper_clean(sp, sp.owner, sp.op)
            load(sp, op)
            pre_ = snap()
            mb, mv = model_handler(op, pre_, sp.owner, buf)
            res = int.from_bytes(mb[STATE_OFF + 0x2C:STATE_OFF + 0x30], 'little')
            kind = 'player' if sp.op == 0x81 else ('record' if not sp.op & 0x80 else 'fn%02X' % (sp.op & 0x7F) if (sp.op & 0x7F) < 12 else 'illegal')
            key = ('0x%02X' % op, kind, 'buf=%s' % ('empty' if not buf else ('1' if len(buf) == 1 else 'many')), 'Result=%d' % res)
        if key in seen:
            continue
        seen[key] = 1
        load(sp, op)
        pre_ = snap()
        v0, _ = call_strict({0x72: H72, 0xA9: HA9, 0xAA: HAA}[op], addr(sp.owner), sp.a1, CELL, STATE)
        post = snap()
        diff = {}
        for off in range(0, len(post), 4):
            if post[off:off + 4] != pre_[off:off + 4]:
                diff['0x%X' % off] = int.from_bytes(post[off:off + 4], 'little')
        gold.append(dict(cls='/'.join(key), op='0x%02X' % op, operand=sp.op, owner_slot=sp.owner, num_entities=sp.num, v0=v0 & M32,
                         result_before=sp.result0,
                         entities_in=[{'state': e.u32(ST_STATE), 'id': e.u32(ST_ID), 'xcoll': e.u32(XCOLL), 'tanim': e.u32(TANIM), 'tdir': e.u32(TDIR),
                                       'lanim': e.u32(LANIM), 'ldir': e.u32(LDIR)} for e in sp.ents],
                         changed_words_after={k: v for k, v in diff.items()}))
    blob = json.dumps(gold, sort_keys=True, indent=0).encode()
    open(os.path.join(HERE, 'ops_goldens.json'), 'wb').write(blob)
    print('goldens: %d vectors over %d classes, sha256 %s' % (len(gold), len(seen), hashlib.sha256(blob).hexdigest()))
sys.exit(1 if (mism or bad_b) else 0)
