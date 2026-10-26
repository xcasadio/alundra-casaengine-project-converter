"""c71_full.py - A19 (0x800619D0, 3 words) and the WHOLE of C71 (0x8007B834..0x8007B998, 89 words) of ALUN_CD.EXE (France) against a Python model written from the disassembly, on the REAL code
(minimal MIPS interpreter of g1a/work/emu, harness h.py: the hero is the slot 0 at 0x80127D30), plus one-word mutants of the real code (in the interpreter's RAM only) killed by the same vectors.
No stub is needed: A19 and C71 call nothing.  The global word 0x8013DA20 (g_clearProgramState, stored by C71 state 4) is compared too.
Entities in the vector: slot 0 = the hero, slot 3 = the drop, slot 5 = a torch candidate, slot 6 = another candidate, slot 7 = a third (types drawn among 0x189 = the torch, 0x187 = a drop, others).
Run: python -B -I -X utf8 c71_full.py [N] [mut]     (N vectors, default 6000; 'mut' = also run the mutants with the first 2500 vectors; 'gold' = write c71_goldens.json)"""
import os, random, struct, sys, json, hashlib
HERE = os.path.dirname(os.path.abspath(__file__))
WORK = os.path.join(HERE, 'work')
sys.path.insert(0, WORK)
sys.path.insert(0, os.path.join(WORK, 'emu'))
sys.path.insert(0, r'C:/Users/casad/AppData/Roaming/Python/Python314/site-packages')
import capstone
from h import World, Ent, ENT0, STRIDE, M32, s32
import emu as _emu

A19, A19_END = 0x800619D0, 0x800619DC
C71, C71_END = 0x8007B834, 0x8007B998
GCLEAR = 0x8013DA20
N = int(sys.argv[1]) if len(sys.argv) > 1 and sys.argv[1].isdigit() else 6000
DO_MUT = 'mut' in sys.argv
HERO = ENT0


def addr(slot):
    return ENT0 + slot * STRIDE


# ---- offsets used (disassembly) ----
STATUS, TYPE, FLAGS, ANIM, CARRIED, ANIMDONE = 0x10, 0x68, 0x6C, 0x88, 0x2C, 0xAC
PROGC, FZ, PZ, SUPPORT, COLLZ, GROUND = 0x54, 0xB8, 0x11C, 0x12C, 0x140, 0x144
B274, B275, C282 = 0x274, 0x275, 0x282


def model_a19(e):
    e.set8(B275, e.u8(PROGC))


def model_c71(ents, e, mem):
    """ents: {address: Ent} of the slots 0, 5, 6, 7; e: the drop (slot 3); mem: {GCLEAR: value}"""
    st = e.u8(B274)
    if st >= 5:
        return
    if st == 0:
        e.set8(B274, 1)
        e.set16(C282, 0x28)
    elif st == 1:
        v = (e.u16(C282) - 1) & 0xFFFF
        e.set16(C282, v)
        if v != 0:
            return
        e.set32(ANIM, 1)
        e.set8(B274, 2)
        e.set32(FLAGS, e.u32(FLAGS) | 0x100)
    elif st == 2:
        if e.u32(COLLZ) == 0 and e.u32(GROUND) == 0:
            return
        e.set32(ANIM, 2)
        e.set8(B274, 3)
    elif st == 3:
        e.set8(B274, 4)
        e.set32(FLAGS, e.u32(FLAGS) & ~0x80 & M32)
        a0 = e.u32(SUPPORT)
        if a0 == HERO:
            a0 = ents[HERO].u32(CARRIED)
        if a0 == 0:
            return
        t = ents[a0]
        if t.u32(TYPE) != 0x189:
            return
        t.set32(STATUS, 3)
        t.set8(B274, 1)
    elif st == 4:
        if e.u32(ANIMDONE) == 0:
            return
        a0 = e.u8(B275)
        e.set8(B274, 0)
        mem[GCLEAR] = 1
        e.set32(ANIM, 0)
        e.set32(FZ, 0)
        e.set32(FLAGS, (e.u32(FLAGS) & ~0x100 & M32) | 0x80)
        e.set32(PROGC, a0)
        e.set32(PZ, (e.u32(PZ) + 0xA00000) & M32)


W = World()
rnd = random.Random(20261026)
SLOTS = (0, 3, 5, 6, 7)


def mk_vector():
    ents = {s: Ent() for s in SLOTS}
    for s in SLOTS:
        e = ents[s]
        for o in range(0, STRIDE, 4):
            e.set32(o, rnd.randrange(1 << 32))
        e.set32(STATUS, rnd.choice([0, 1, 2, 2, 3, 4]))
    d = ents[3]
    d.set8(B274, rnd.choice([0, 1, 2, 3, 4, 5, 6, 255, 0, 1, 2, 3, 4]))
    d.set16(C282, rnd.choice([0, 1, 2, 40, 0xFFFF, 0x8001, 0x8000, rnd.randrange(1 << 16), 1, 1]))
    d.set32(COLLZ, rnd.choice([0, 0, 1, rnd.randrange(1 << 32)]))
    d.set32(GROUND, rnd.choice([0, 0, 1, rnd.randrange(1 << 32)]))
    d.set32(ANIMDONE, rnd.choice([0, 1, 1, 2, rnd.randrange(1 << 32)]))
    d.set32(FLAGS, rnd.choice([0x32080, 0x32180, 0x32100, rnd.randrange(1 << 32)]))
    d.set32(PROGC, rnd.choice([0, 0xB1, rnd.randrange(1 << 32)]))
    d.set8(B275, rnd.choice([0, 0xB1, rnd.randrange(256)]))
    # the support (+0x12C): 0, the hero, one of the three candidates (all valid entity addresses: a wild pointer would write anywhere)
    d.set32(SUPPORT, rnd.choice([0, HERO, HERO, HERO, addr(5), addr(6), addr(7), addr(3)]))
    h = ents[0]
    h.set32(CARRIED, rnd.choice([0, addr(5), addr(5), addr(6), addr(7), HERO, addr(3)]))
    for s in (5, 6, 7):
        ents[s].set32(TYPE, rnd.choice([0x189, 0x189, 0x187, rnd.randrange(1 << 12), 0x188, 0x18A]))
    ents[3].set32(TYPE, 0x187)
    return ents


def run_real(fn, ents):
    for s in SLOTS:
        W.put(s, ents[s])
    W.m.ww(GCLEAR, 0x12345678)
    W.j.clear()
    W.call(fn, addr(3))
    return {s: bytes(W.get(s).raw) for s in SLOTS}, W.m.rw(GCLEAR), list(W.j)


def run_model(fn, ents):
    me = {s: ents[s].copy() for s in SLOTS}
    mem = {GCLEAR: 0x12345678}
    amap = {addr(s): me[s] for s in SLOTS}
    if fn == 'a19':
        model_a19(me[3])
    else:
        model_c71(amap, me[3], mem)
    return {s: bytes(me[s].raw) for s in SLOTS}, mem[GCLEAR], []


# ---- coverage ----
COV = set()
_o = _emu.Emu.exec1


def _x(self, pc, wd):
    COV.add(pc)
    return _o(self, pc, wd)


_emu.Emu.exec1 = _x

vectors = [mk_vector() for _ in range(N)]


def check(vecs, fn, real_addr):
    bad = 0
    for v in vecs:
        try:
            r = run_real(real_addr, v)
        except Exception:
            return 1
        if r != run_model(fn, v):
            bad += 1
    return bad


mism_a = check(vectors, 'a19', A19)
mism_c = 0
first_bad = None
for v in vectors:
    r = run_real(C71, v)
    m = run_model('c71', v)
    if r != m:
        mism_c += 1
        if first_bad is None:
            first_bad = (v, r, m)
miss = [p for p in range(C71, C71_END, 4) if p not in COV]
missa = [p for p in range(A19, A19_END, 4) if p not in COV]
print('A19 (3 words): %d vectors, %d mismatches, instruction coverage %d/3' % (N, mism_a, 3 - len(missa)))
print('C71 whole function: %d vectors, %d mismatches, instruction coverage %d/%d, uncovered %s' % (N, mism_c, (C71_END - C71) // 4 - len(miss), (C71_END - C71) // 4, ' '.join('%08x' % p for p in miss)))
if first_bad:
    v, r, m = first_bad
    for s in SLOTS:
        for o in range(0, STRIDE, 4):
            if r[0][s][o:o + 4] != m[0][s][o:o + 4]:
                print('  slot %d +%X real %s model %s' % (s, o, r[0][s][o:o + 4].hex(), m[0][s][o:o + 4].hex()))
    print('  global real %08x model %08x' % (r[1], m[1]))


def classify(v):
    d = v[3]
    st = d.u8(B274)
    if st >= 5:
        return 'state>=5'
    if st == 3:
        sup = d.u32(SUPPORT)
        t = sup
        if sup == HERO:
            t = v[0].u32(CARRIED)
        if t == 0:
            return 'st3 no object'
        return 'st3 torch hit' if v[(t - ENT0) // STRIDE].u32(TYPE) == 0x189 else 'st3 other type'
    if st == 1:
        return 'st1 counter->0' if ((d.u16(C282) - 1) & 0xFFFF) == 0 else 'st1 counting'
    if st == 2:
        return 'st2 contact' if (d.u32(COLLZ) or d.u32(GROUND)) else 'st2 falling'
    if st == 4:
        return 'st4 restore' if d.u32(ANIMDONE) else 'st4 waiting'
    return 'st0 arm'


cls = {}
for v in vectors:
    k = classify(v)
    cls[k] = cls.get(k, 0) + 1
print('vector classes: %s' % dict(sorted(cls.items())))

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
    for label, lo, hi, fn in (('A19', A19, A19_END, 'a19'), ('C71', C71, C71_END, 'c71')):
        real_addr = A19 if fn == 'a19' else C71
        total = killed = 0
        surv = []
        for a in range(lo, hi, 4):
            w = W.m.rw(a)
            for name, nw in mutants(w):
                total += 1
                W.m.ww(a, nw)
                try:
                    dead = check(sub, fn, real_addr) != 0
                except Exception:
                    dead = True
                W.m.ww(a, w)
                if dead:
                    killed += 1
                else:
                    surv.append((a, name, dis(a, w), dis(a, nw)))
        print('%s mutants %d killed %d survivors %d' % (label, total, killed, len(surv)))
        for s in surv:
            print('  SURVIVOR %08x %-16s %-28s -> %s' % s)

if 'gold' in sys.argv:
    seen = {}
    gold = []
    for v in vectors:
        k = classify(v)
        if seen.get(k, 0) >= 2:
            continue
        seen[k] = seen.get(k, 0) + 1
        r = run_real(C71, v)
        READ = [STATUS, TYPE, FLAGS, ANIM, CARRIED, ANIMDONE, PROGC, FZ, PZ, SUPPORT, COLLZ, GROUND]
        gold.append(dict(cls=k, drop_in={'0x%X' % o: v[3].u32(o) for o in READ} | {'0x274': v[3].u8(B274), '0x275': v[3].u8(B275), '0x282': v[3].u16(C282)},
                         hero_carried=v[0].u32(CARRIED), cand_types={s: v[s].u32(TYPE) for s in (5, 6, 7)},
                         drop_out_changed={'0x%X' % o: Ent(r[0][3]).u32(o) for o in range(0, STRIDE, 4) if Ent(r[0][3]).u32(o) != v[3].u32(o)},
                         others_changed={s: {'0x%X' % o: Ent(r[0][s]).u32(o) for o in range(0, STRIDE, 4) if Ent(r[0][s]).u32(o) != v[s].u32(o)} for s in (0, 5, 6, 7) if r[0][s] != bytes(v[s].raw)},
                         global_clear=r[1]))
    blob = json.dumps(gold, sort_keys=True, indent=0).encode()
    open(os.path.join(HERE, 'c71_goldens.json'), 'wb').write(blob)
    print('goldens: %d vectors over %d classes, sha256 %s' % (len(gold), len(seen), hashlib.sha256(blob).hexdigest()))
sys.exit(1 if (mism_a or mism_c) else 0)
