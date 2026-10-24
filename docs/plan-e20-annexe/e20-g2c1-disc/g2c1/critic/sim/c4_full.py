"""c4_full.py - the WHOLE of C4 (0x80066984..0x80066BF4, carried half AND free half) against a Python model written from the disassembly, on the REAL code
(minimal MIPS interpreter of g1a/work/emu), plus one-word mutants of the real code (in the interpreter's RAM only) killed by the same vectors.
Stubs journalled by h.World: PlaySoundEffect 0x800490FC, CreateEffectEntity 0x8003BDD8 (everything else is the real code; C4 calls nothing else).
Run: python -B -I c4_full.py [N] [mut]     (N vectors, default 4000; 'mut' = also run the mutants with the first 1500 vectors)"""
import os, random, struct, sys
HERE = os.path.dirname(os.path.abspath(__file__))
WORK = os.path.join(HERE, 'work')
sys.path.insert(0, WORK)
sys.path.insert(0, os.path.join(WORK, 'emu'))
import capstone
import g1a_models as M
from h import World, Ent, ENT0, STRIDE, M32, s32
import emu as _emu

C4 = 0x80066984
C4_END = 0x80066BF8
TILES = 0x801E0000            # scratch table of 8 u16 tile words (harness write: the four corner pointers +0x148..+0x154 point into it)
N = int(sys.argv[1]) if len(sys.argv) > 1 and sys.argv[1].isdigit() else 4000
DO_MUT = 'mut' in sys.argv

# ---- entity offsets read or written by C4 (all from the disassembly) ----
STATUS, T24, CARRIER, TYPE, FLAGS, MODE80, ANIM, DIR = 0x10, 0x24, 0x28, 0x68, 0x6C, 0x80, 0x88, 0x8C
FZ, PX, PY, PZ = 0xB8, 0x114, 0x118, 0x11C
ADJ, GROUND, TILEP0, SLOPE, HITC, BOUNCED = 0x13C, 0x144, 0x148, 0x18C, 0x220, 0x278
TBL = 0x800270C0              # the release table of C4 (+0x24 -> animation), identical to C23 and C70 (0x80027400, 0x800281DC)


def model(e: Ent, hero: Ent, tiles, j):
    """returns nothing; mutates e and hero, appends to j the journalled calls in order"""
    def tile_hits():
        return [(tiles[(e.u32(TILEP0 + 4 * i) - TILES) // 2] & 0x1001) == 0x1001 for i in range(4)]
    if e.u32(CARRIER) != 0:
        t = e.s32(T24)
        if t == 0:
            e.set32(ANIM, 0)
            return
        if t == -1:
            e.set32(ANIM, 3)
            if e.u32(MODE80) == 2:
                e.set32(FLAGS, ((e.u32(FLAGS) | 0x10) & ~0x80) & M32)
        else:
            e.set32(ANIM, W.m.rw(TBL + 4 * t))
        e.set32(T24, 0)
        hero.set32(M.CARRIED, 0)
        e.set32(CARRIER, 0)
        return
    anim = e.u32(ANIM)
    skip_to_tail = False
    if anim != 0:
        if e.u32(TYPE) == 0x175 and anim == 5:
            pass                                                  # straight to the 0x175 block (0x80066B90)
        elif e.u32(ADJ) != 0:
            if e.u32(GROUND) == 0:                                # blocked while airborne: the bounce (0x80066A68)
                if e.s32(FZ) > 0 and e.u32(BOUNCED) == 0:
                    e.set32(BOUNCED, 1)
                    e.set32(DIR, (e.u32(DIR) + 0x10) & 0x1F)
                    j.append(('effect', 0, 9, 0, e.u32(PX), e.u32(PY), (e.u32(PZ) + 0x80000) & M32))
            else:                                                 # blocked on the ground (0x80066AD0)
                if any(tile_hits()):
                    e.set32(STATUS, 3)
                e.set32(ANIM, 0)
        else:                                                     # not blocked (0x80066B14)
            if e.u32(GROUND) != 0 or e.u32(HITC) != 0:
                j.append(('sound', 0x18))
                e.set32(BOUNCED, 0)
                if not any(tile_hits()):
                    e.set32(ANIM, 0)
    if e.u32(TYPE) == 0x175:                                      # the floating log block (0x80066B88)
        if e.u32(ANIM) == 0:
            if e.s32(SLOPE) == 4:
                e.set32(ANIM, 5)
                return
        if e.u32(ANIM) == 5 and e.s32(SLOPE) != 4:
            e.set32(ANIM, 0)


W = World()
rnd = random.Random(20261022)
for i in range(8):
    pass


def mk():
    e = Ent()
    e.set32(STATUS, rnd.choice([2, 2, 2, 3, 4]))
    e.set32(CARRIER, rnd.choice([0, 0, 0, ENT0]))
    e.set32(T24, rnd.choice([0, 1, 2, 3, 0xFFFFFFFF, 4, 5, 0xFFFFFFFE, 0, 1, 2, 3]))
    e.set32(TYPE, rnd.choice([0x175, 0x175, 0x122, 0x125, 0x16B, rnd.randrange(1 << 12)]))
    e.set32(FLAGS, rnd.randrange(1 << 24))
    e.set32(MODE80, rnd.choice([0, 1, 2, 2, 3]))
    e.set32(ANIM, rnd.choice([0, 0, 1, 2, 3, 4, 5, 5, 5, 6, rnd.randrange(1 << 32)]))
    e.set32(DIR, rnd.randrange(1 << 32))
    e.set32(FZ, rnd.choice([0, 1, -1, 0x10000, -0x10000, 0x7FFFFFFF, 0x80000000, rnd.randrange(1 << 32)]) & M32)
    for o in (PX, PY, PZ):
        e.set32(o, rnd.randrange(1 << 32))
    e.set32(ADJ, rnd.choice([0, 0, 1, 1, rnd.randrange(1 << 32)]))
    e.set32(GROUND, rnd.choice([0, 1, 1, rnd.randrange(1 << 32)]))
    e.set32(SLOPE, rnd.choice([4, 4, 0, 1, 2, 3, 5, 0xFFFFFFFF]))
    e.set32(HITC, rnd.choice([0, 0, 1, 3, rnd.randrange(1 << 32)]))
    e.set32(BOUNCED, rnd.choice([0, 0, 1, rnd.randrange(1 << 32)]))
    for i in range(5):                       # the fifth pointer (+0x158) is never read by C4: a valid one kills the 'slti 4->5' mutants
        e.set32(TILEP0 + 4 * i, TILES + 2 * rnd.randrange(8))
    return e


def hero():
    h = Ent()
    h.set32(M.CARRIED, ENT0 + 3 * STRIDE)
    for o in (M.OFFX, M.OFFY, M.OFFZ, M.ANIM):
        h.set32(o, rnd.randrange(1 << 32))
    return h


def set_tiles():
    words = [0x1001, 0x1001, 0x1000, 0x0001, 0x1003, 0x9001, 0x0040, rnd.randrange(1 << 16)]
    rnd.shuffle(words)
    for i, v in enumerate(words):
        W.m.wh(TILES + 2 * i, v)
    return words


# ---- coverage ----
COV = set()
_o = _emu.Emu.exec1


def _x(self, pc, wd):
    COV.add(pc)
    return _o(self, pc, wd)


_emu.Emu.exec1 = _x


def run_one(e, h, words):
    """REAL code on the images; returns (raw hero, raw entity, journal)"""
    W.j.clear()
    W.put(0, h)
    W.put(3, e)
    W.call(C4, ENT0 + 3 * STRIDE)
    return bytes(W.get(0).raw), bytes(W.get(3).raw), list(W.j)


def model_one(e, h, words):
    me, mh = e.copy(), h.copy()
    j = []
    model(me, mh, words, j)
    return bytes(mh.raw), bytes(me.raw), j


vectors = []
for k in range(N):
    words = set_tiles()
    vectors.append((mk(), hero(), list(words)))


def apply_words(words):
    for i, v in enumerate(words):
        W.m.wh(TILES + 2 * i, v)


def check(vecs):
    bad = 0
    for e, h, words in vecs:
        apply_words(words)
        try:
            r = run_one(e, h, words)
        except Exception:
            return 1
        if r != model_one(e, h, words):
            bad += 1
    return bad


mism = 0
first_bad = None
for e, h, words in vectors:
    apply_words(words)
    r = run_one(e, h, words)
    m = model_one(e, h, words)
    if r != m:
        mism += 1
        if first_bad is None:
            first_bad = (e, h, words, r, m)
lo, hi = C4, C4_END
miss = [p for p in range(lo, hi, 4) if p not in COV]
print('C4 whole function: %d vectors, %d mismatches, instruction coverage %d/%d, uncovered %s' % (N, mism, (hi - lo) // 4 - len(miss), (hi - lo) // 4, ' '.join('%08x' % p for p in miss)))
if first_bad:
    e, h, words, r, m = first_bad
    print('first mismatch: journal real %s model %s' % (r[2], m[2]))
    for o in range(0, STRIDE, 4):
        if r[1][o:o + 4] != m[1][o:o + 4]:
            print('  entity +%X real %s model %s' % (o, r[1][o:o + 4].hex(), m[1][o:o + 4].hex()))
# outcome classes seen
cls = {}
for e, h, words in vectors:
    apply_words(words)
    r = model_one(e, h, words)
    me = Ent(r[1])
    key = ('carried' if e.u32(CARRIER) else 'free',
           'anim0' if e.u32(ANIM) == 0 else 'anim!0',
           'status3' if me.u32(STATUS) == 3 and e.u32(STATUS) != 3 else '',
           ','.join(sorted(set(x[0] for x in r[2]))))
    cls[key] = cls.get(key, 0) + 1
print('model outcome classes (carried/free, anim, status set, journal kinds) over the vectors:')
for k, v in sorted(cls.items()):
    print('  ', k, v)

if DO_MUT:
    md = capstone.Cs(capstone.CS_ARCH_MIPS, capstone.CS_MODE_MIPS32 + capstone.CS_MODE_LITTLE_ENDIAN)

    def dis(addr, w):
        for i in md.disasm(struct.pack('<I', w), addr):
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
            out.append(('andi mask>>1', (w & ~0xFFFF) | (imm >> 1)))
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
        return out

    sub = vectors[:1500]
    total = killed = 0
    surv = []
    for addr in range(C4, C4_END, 4):
        w = W.m.rw(addr)
        for name, nw in mutants(w):
            total += 1
            W.m.ww(addr, nw)
            try:
                dead = check(sub) != 0
            except Exception:
                dead = True
            W.m.ww(addr, w)
            if dead:
                killed += 1
            else:
                surv.append((addr, name, dis(addr, w), dis(addr, nw)))
    print('mutants %d killed %d survivors %d' % (total, killed, len(surv)))
    for s in surv:
        print('  SURVIVOR %08x %-16s %-28s -> %s' % s)

if 'gold' in sys.argv:
    import json, hashlib
    READ = [STATUS, T24, CARRIER, TYPE, FLAGS, MODE80, ANIM, DIR, FZ, PX, PY, PZ, ADJ, GROUND, HITC, BOUNCED, SLOPE] + [TILEP0 + 4 * i for i in range(4)]
    seen = {}
    gold = []
    for e, h, words in vectors:
        apply_words(words)
        r = run_one(e, h, words)                      # REAL code
        me = Ent(r[1])
        t24 = e.s32(T24)
        key = ('carried' if e.u32(CARRIER) else 'free', ('t24=%d' % t24 if t24 in (0, -1) else 't24>0') if e.u32(CARRIER) else '',
               'anim0' if e.u32(ANIM) == 0 else ('anim5' if e.u32(ANIM) == 5 else 'anim?'), 'log' if e.u32(TYPE) == 0x175 else 'std',
               'adj' if e.u32(ADJ) else '', 'gnd' if e.u32(GROUND) else 'air', 'hit' if e.u32(HITC) else '', 'rise' if e.s32(FZ) > 0 else '', 'bounced' if e.u32(BOUNCED) else '',
               'slope4' if e.s32(SLOPE) == 4 else '', ','.join(sorted(set(x[0] for x in r[2]))), 'status3' if me.u32(STATUS) == 3 and e.u32(STATUS) != 3 else '')
        if seen.get(key, 0) >= 1:
            continue
        seen[key] = seen.get(key, 0) + 1
        gold.append(dict(cls='/'.join(key), tiles=words, inp={'0x%X' % o: e.u32(o) for o in READ}, hero_carried_before=h.u32(M.CARRIED),
                         out={'0x%X' % o: me.u32(o) for o in range(0, STRIDE, 4) if me.u32(o) != e.u32(o)}, hero_carried_after=Ent(r[0]).u32(M.CARRIED), journal=[list(x) for x in r[2]]))
    blob = json.dumps(gold, sort_keys=True, indent=0).encode()
    open(os.path.join(HERE, 'c4_goldens.json'), 'wb').write(blob)
    print('goldens: %d vectors over %d classes, sha256 %s' % (len(gold), len(seen), hashlib.sha256(blob).hexdigest()))
sys.exit(1 if mism else 0)
