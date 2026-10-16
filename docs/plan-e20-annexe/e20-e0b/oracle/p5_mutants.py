"""p5_mutants.py - CRITIC mutation battery of my own on the loot roll (0x80032968-0x800329FC ChooseRandomlyAnItem, 0x80032A40-0x80032B24 roll).
Generic one-word mutations (immediate bit flips and +-1, beq<->bne, bgez<->bltz, mult<->multu swap kept out), applied in machine RAM only.
A mutant is CAUGHT by a vector set when its signature (hash of all outputs) differs from the unpatched one.
Two vector sets are compared: the DISCOVERY's (out/choose_vectors.json 2626 + out/roll_vectors.json 52, from the binary report) and MINE (the 6000 fuzz vectors of p1_roll.py, out/roll_fuzz_vectors.json,
plus the 256-code table sweep).  Survivors of both are listed with their disassembly so equivalence can be judged by hand."""
import sys, os, json, hashlib, struct
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, 'tools'))
sys.path.insert(0, HERE)
from machine import Machine, M32, s32
from cdis import D, off, md

RNG = 0x80098708
E, REC, HDR = 0x80180000, 0x80180400, 0x80180500
BANK_P, BANK_T = 0x801EB344, 0x801EBA40
BIN = os.path.join(HERE, 'out')      # E20.e0b2 T0: the vectors of the annex (the discovery's were in its scratchpad)
CHOOSE = json.load(open(BIN + '/choose_vectors.json'))
ROLL = json.load(open(BIN + '/roll_vectors.json'))
FUZZ = json.load(open(os.path.join(HERE, 'out', 'roll_fuzz_vectors.json')))['vectors'][:2000]


def roll_once(m, has_rec, flag, rec_code, hdr_code, s, pers, temp):
    for a in range(0, 0x300, 4):
        m.poke(E + a, 0)
    for a in range(0, 0x40, 4):
        m.poke(REC + a, 0)
        m.poke(HDR + a, 0)
    for b in (BANK_P, BANK_T):
        for a in range(0, 0x100, 4):
            m.poke(b + a, 0)
    for o, v in pers:
        m.poke(BANK_P + o, v)
    for o, v in temp:
        m.poke(BANK_T + o, v)
    m.poke(E + 0x64, HDR)
    m.poke(HDR + 0x1C, hdr_code << 24)
    if has_rec:
        m.poke(E + 0x44, REC)
        m.poke(REC + 0x10, (flag & 0xFFFF) | (rec_code << 16))
    m.poke(E + 0x3C, 0xA5A5A5A5)
    m.poke(E + 0x40, 0xA5A5A5A5)
    m.poke(RNG, s)
    m.call(0x80032A40, E, limit=20000)
    return s32(m.peek(E + 0x3C)), s32(m.peek(E + 0x40)), m.peek(RNG)


def sig(patch, which):
    m = Machine()
    m.watch_uninit = False
    if patch:
        a, new = patch
        m.poke(a, new)
    h = hashlib.sha1()
    try:
        if which == 'disc':
            for code, s, _, _, _ in CHOOSE:
                m.poke(RNG, s)
                h.update(('%d,%d;' % (m.call(0x80032968, code, limit=20000) & M32, m.peek(RNG))).encode())
            for v in ROLL:
                bk = [(b[1] * 4, b[2]) if b[0] == BANK_P else None for b in v['banks']]
                pers = [(b[1] * 4, b[2]) for b in v['banks'] if b[0] == BANK_P]
                temp = [(b[1] * 4, b[2]) for b in v['banks'] if b[0] == BANK_T]
                h.update(('%s;' % (roll_once(m, v['rec'], v['flag'] or 0, v['rec_code'] or 0, v['header_contents'], v['rng_in'], pers, temp),)).encode())
        else:
            for hr, fl, rc, hc, s, pers, temp, *_ in FUZZ:
                h.update(('%s;' % (roll_once(m, hr, fl, rc, hc, s, pers, temp),)).encode())
            for code in range(512):      # E20.e0b2 T0: codes 0x100..0x1FF added
                for seed in (0x12345678, 0x4F1427FB, 0x985CF02A, 0x5EC362FE, 0x0FFFFFFF, 0xF0000000):
                    m.poke(RNG, seed)
                    h.update(('%d,%d;' % (m.call(0x80032968, code, limit=20000) & M32, m.peek(RNG))).encode())
    except Exception as ex:      # a mutant that loops or faults is CAUGHT by definition
        return 'EXC:' + type(ex).__name__
    return h.hexdigest()


def mutants():
    out = []
    ranges = [(0x80032968, 0x80032A00), (0x80032A40, 0x80032B28)]
    for lo, hi in ranges:
        for a in range(lo, hi, 4):
            w = struct.unpack_from('<I', D, off(a))[0]
            op = w >> 26
            imm = w & 0xFFFF
            if op in (0x04, 0x05):           # beq <-> bne
                out.append((a, w ^ (1 << 26), 'beq/bne'))
            elif op == 0x01:                  # regimm: bgez(1) <-> bltz(0)
                out.append((a, w ^ (1 << 16), 'bgez/bltz'))
            elif op in (0x09, 0x0A, 0x0B, 0x0C, 0x0D, 0x23, 0x24, 0x25, 0x2B, 0x0F):   # addiu slti sltiu andi ori lw lbu lhu sw lui
                out.append((a, (w & ~0xFFFF) | (imm ^ 1), 'imm^1'))
                out.append((a, (w & ~0xFFFF) | ((imm + 1) & 0xFFFF), 'imm+1'))
                out.append((a, (w & ~0xFFFF) | ((imm - 1) & 0xFFFF), 'imm-1'))
            elif op == 0 and (w & 0x3F) in (0x00, 0x02, 0x03):   # sll srl sra: shift amount +-1
                sa = (w >> 6) & 31
                out.append((a, (w & ~(31 << 6)) | (((sa + 1) & 31) << 6), 'sa+1'))
                out.append((a, (w & ~(31 << 6)) | (((sa - 1) & 31) << 6), 'sa-1'))
    return out


base_d = sig(None, 'disc')
base_m = sig(None, 'mine')
res = []
for a, new, kind in mutants():
    old = struct.unpack_from('<I', D, off(a))[0]
    if new == old:
        continue
    sd = sig((a, new), 'disc')
    sm = sig((a, new), 'mine')
    res.append((a, kind, old, new, sd != base_d, sm != base_m))

n = len(res)
cd = sum(1 for r in res if r[4])
cm = sum(1 for r in res if r[5])
both = sum(1 for r in res if r[4] or r[5])
print('mutants %d; caught by the discovery vectors %d; by my vectors %d; by either %d' % (n, cd, cm, both))
print('caught by mine but NOT by the discovery vectors:')
for a, kind, old, new, c1, c2 in res:
    if c2 and not c1:
        ins = next(md.disasm(struct.pack('<I', old), a))
        print('   %08X %-9s %-8s %s' % (a, kind, ins.mnemonic, ins.op_str))
print('caught by the discovery vectors but NOT by mine:')
for a, kind, old, new, c1, c2 in res:
    if c1 and not c2:
        ins = next(md.disasm(struct.pack('<I', old), a))
        print('   %08X %-9s %-8s %s' % (a, kind, ins.mnemonic, ins.op_str))
print('SURVIVORS of both sets (to judge equivalence by hand):')
for a, kind, old, new, c1, c2 in res:
    if not c1 and not c2:
        ins = next(md.disasm(struct.pack('<I', old), a))
        mi = next(md.disasm(struct.pack('<I', new), a))
        print('   %08X %-9s %-8s %-22s -> %-8s %s' % (a, kind, ins.mnemonic, ins.op_str, mi.mnemonic, mi.op_str))
