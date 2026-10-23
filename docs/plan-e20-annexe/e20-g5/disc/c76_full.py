"""c76_full.py - the WHOLE of C76 (the pushable pillar, 0x8007C024..0x8007C0D4, 43 words) AND of its helper GetPushablePillarPushDirection (0x8003AC9C..0x8003AD30, 37 words)
against a Python model written from the disassembly, on the REAL code (minimal MIPS interpreter of work/emu), plus one-word mutants of the real code (in the interpreter's RAM
only) killed by the same vectors, plus goldens.
Stubs journalled: PlaySoundEffect 0x800490FC ('sound', id) and StopSoundEffect 0x80049634 ('stop', id) - the latter is NEW here (the decompilation calls PlaySoundEffect again).
Run: python -B -I c76_full.py [N] [mut] [gold]"""
import os, random, struct, sys, json, hashlib
HERE = os.path.dirname(os.path.abspath(__file__))
WORK = os.path.join(HERE, 'work')
sys.path.insert(0, WORK)
sys.path.insert(0, os.path.join(WORK, 'emu'))
import capstone
from h import World, Ent, ENT0, STRIDE, M32, s32
import emu as _emu

C76, C76_END = 0x8007C024, 0x8007C0D8
HLP, HLP_END = 0x8003AC9C, 0x8003AD30
N = int(sys.argv[1]) if len(sys.argv) > 1 and sys.argv[1].isdigit() else 6000
DO_MUT = 'mut' in sys.argv
PILLAR_SLOT = 3
PADDR = ENT0 + PILLAR_SLOT * STRIDE
# entity offsets read or written (all from the disassembly)
ANIM_T, DIR_T, XCOLL, PX, PY, MX, MY, WID, CNT = 0x88, 0x8C, 0x130, 0x114, 0x118, 0x1D8, 0x1DC, 0x1F0, 0x274

W = World()
j = W.j


def stop(m):
    W.j.append(('stop', m.r[4] & M32))


W.m.hooks[0x80049634] = stop


def push_dir(e, h):
    """GetPushablePillarPushDirection 0x8003AC9C(a0=pillar e, a1=hero h) -> int (signed)"""
    if h.u32(XCOLL) != PADDR:
        return -1
    dx = s32(h.u32(MX) - e.u32(MX))
    if dx < 0:
        side = h.s32(WID) < s32(-dx)
    else:
        side = e.s32(WID) < dx
    if not side:
        return (1 if h.s32(PY) < e.s32(PY) else 0) << 4
    return 8 if h.s32(PX) < e.s32(PX) else 0x18


def model(e, h, jj):
    """C76 0x8007C024(a0=e); the hero is the entity of slot 0 (0x80127D30)"""
    if h.u32(XCOLL) == PADDR:
        t = e.s32(CNT)
        if t < 0x1f:
            e.set32(CNT, t + 1)
            return
        jj.append(('sound', 0x19))
        e.set32(ANIM_T, 1)
        r = push_dir(e, h)
        if r != -1:
            e.set32(DIR_T, (r + 0x10) & 0x1f)
            return
    if e.u32(ANIM_T) == 1:
        jj.append(('stop', 0x19))
    e.set32(CNT, 0)
    e.set32(ANIM_T, 0)


rnd = random.Random(20261023)


def mk():
    e = Ent()
    e.set32(ANIM_T, rnd.choice([0, 0, 1, 1, 2, rnd.randrange(1 << 32)]))
    e.set32(DIR_T, rnd.randrange(1 << 32))
    e.set32(CNT, rnd.choice([0, 1, 29, 30, 31, 32, 40, 0xFFFFFFFF, 0x80000000, 0x7FFFFFFF, rnd.randrange(1 << 32)]))
    base = rnd.randrange(1 << 31)
    e.set32(MX, base)
    e.set32(PX, rnd.randrange(1 << 32))
    e.set32(PY, rnd.randrange(1 << 32))
    e.set32(WID, rnd.choice([0, 0x13FFFF, 0x13FFFF, 0x140000, 0x1D0000, rnd.randrange(1 << 32)]))
    return e


def hero_for(e):
    h = Ent()
    h.set32(XCOLL, rnd.choice([PADDR, PADDR, PADDR, 0, ENT0 + 5 * STRIDE, ENT0, rnd.randrange(1 << 32)]))
    W_e, W_h = e.s32(WID), rnd.choice([0, 0x13FFFF, 0x1D0000, rnd.randrange(1 << 20)])
    h.set32(WID, W_h)
    kind = rnd.choice(['pos_edge_e', 'neg_edge_h', 'pos_edge_e1', 'neg_edge_h1', 'rand', 'zero', 'wrap'])
    ex = e.u32(MX)
    if kind == 'pos_edge_e':
        dx = W_e
    elif kind == 'pos_edge_e1':
        dx = W_e + 1
    elif kind == 'neg_edge_h':
        dx = -W_h
    elif kind == 'neg_edge_h1':
        dx = -W_h - 1
    elif kind == 'zero':
        dx = 0
    elif kind == 'wrap':
        dx = rnd.choice([0x80000000, 0x7FFFFFFF, 0xFFFFFFFF])
    else:
        dx = rnd.randrange(-(1 << 22), 1 << 22)
    h.set32(MX, (ex + dx) & M32)
    # relative X/Y of the position words: equal, one apart, or random
    h.set32(PX, rnd.choice([e.u32(PX), (e.u32(PX) + 1) & M32, (e.u32(PX) - 1) & M32, rnd.randrange(1 << 32)]))
    h.set32(PY, rnd.choice([e.u32(PY), (e.u32(PY) + 1) & M32, (e.u32(PY) - 1) & M32, rnd.randrange(1 << 32)]))
    return h


COV = set()
_o = _emu.Emu.exec1


def _x(self, pc, wd):
    COV.add(pc)
    return _o(self, pc, wd)


_emu.Emu.exec1 = _x


def run_one(e, h):
    W.j.clear()
    W.put(0, h)
    W.put(PILLAR_SLOT, e)
    W.call(C76, PADDR)
    return bytes(W.get(0).raw), bytes(W.get(PILLAR_SLOT).raw), list(W.j)


def model_one(e, h):
    me, mh = e.copy(), h.copy()
    jj = []
    model(me, mh, jj)
    return bytes(mh.raw), bytes(me.raw), jj


vectors = []
for k in range(N):
    e = mk()
    vectors.append((e, hero_for(e)))


def check(vecs):
    bad = 0
    for e, h in vecs:
        try:
            r = run_one(e, h)
        except Exception:
            return 1
        if r != model_one(e, h):
            bad += 1
    return bad


mism = 0
first_bad = None
for e, h in vectors:
    r = run_one(e, h)
    m = model_one(e, h)
    if r != m:
        mism += 1
        if first_bad is None:
            first_bad = (e, h, r, m)
miss = [p for p in list(range(C76, C76_END, 4)) + list(range(HLP, HLP_END, 4)) if p not in COV]
print('C76 + helper: %d vectors, %d mismatches, instruction coverage %d/%d, uncovered %s' % (
    N, mism, (C76_END - C76) // 4 + (HLP_END - HLP) // 4 - len(miss), (C76_END - C76) // 4 + (HLP_END - HLP) // 4, ' '.join('%08x' % p for p in miss)))
if first_bad:
    e, h, r, m = first_bad
    print('first mismatch: journal real %s model %s' % (r[2], m[2]))
    for o in range(0, STRIDE, 4):
        if r[1][o:o + 4] != m[1][o:o + 4]:
            print('  entity +%X real %s model %s' % (o, r[1][o:o + 4].hex(), m[1][o:o + 4].hex()))
cls = {}
for e, h in vectors:
    r = model_one(e, h)
    me = Ent(r[1])
    key = ('contact' if h.u32(XCOLL) == PADDR else 'no-contact',
           'cnt<31' if e.s32(CNT) < 31 else 'cnt>=31', 'anim%s' % ('1' if e.u32(ANIM_T) == 1 else '!1'), ','.join(x[0] + str(x[1]) for x in r[2]),
           'dir=%d' % me.u32(DIR_T) if me.u32(DIR_T) != e.u32(DIR_T) else '')
    cls[key] = cls.get(key, 0) + 1
print('model outcome classes: %d' % len(cls))
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

    sub = vectors[:2500]
    total = killed = 0
    surv = []
    for lo, hi in ((C76, C76_END), (HLP, HLP_END)):
        for addr in range(lo, hi, 4):
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
    seen = {}
    gold = []
    READ = [ANIM_T, DIR_T, XCOLL, PX, PY, MX, WID, CNT]
    for e, h in vectors:
        r = run_one(e, h)
        me = Ent(r[1])
        side = None
        if h.u32(XCOLL) == PADDR and e.s32(CNT) >= 31:
            side = push_dir(e, h)
        key = ('contact' if h.u32(XCOLL) == PADDR else 'other', 'cnt>=31' if e.s32(CNT) >= 31 else 'cnt<31', 'anim1' if e.u32(ANIM_T) == 1 else 'anim!1',
               'pd=%s' % side, ','.join(x[0] for x in r[2]))
        if key in seen:
            continue
        seen[key] = 1
        gold.append(dict(cls='/'.join(key), pillar_in={'0x%X' % o: e.u32(o) for o in READ}, hero_in={'0x%X' % o: h.u32(o) for o in READ},
                         pillar_out={'0x%X' % o: me.u32(o) for o in range(0, STRIDE, 4) if me.u32(o) != e.u32(o)}, journal=[list(x) for x in r[2]]))
    blob = json.dumps(gold, sort_keys=True, indent=0).encode()
    open(os.path.join(HERE, 'c76_goldens.json'), 'wb').write(blob)
    print('goldens: %d vectors over %d classes, sha256 %s' % (len(gold), len(seen), hashlib.sha256(blob).hexdigest()))
sys.exit(1 if mism else 0)
