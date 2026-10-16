"""s3_roll.py - the loot roll 0x80032A40 on the REAL code (MIPS machine), called directly on a hand-built entity.
Inputs: [e+0x44] record pointer (0 = none), [e+0x64] sprite header pointer, record hu +0x10 (game flag) and byte +0x12 (code), header byte +0x1F (Contents), the two flag banks
0x801EB344 (persistent, flag bit 0x8000 clear) and 0x801EBA40 (temporary, bit 0x8000 set), the RNG word.  Outputs: [e+0x3C], [e+0x40], RNG word, every other RAM byte that changed.
Output: out/roll_vectors.json and a text table."""
import sys
import json
sys.path.insert(0, 'tools')
sys.path.insert(0, '.')
from machine import Machine, M32, s32

RNG = 0x80098708
E, REC, HDR = 0x80180000, 0x80180400, 0x80180500
BANK_P, BANK_T = 0x801EB344, 0x801EBA40


def lcg(s):
    return (s * 0x7D2B89DD + 0xE06A02E7) & M32


m = Machine()
m.watch_uninit = False


def reset(banks=None):
    for a in range(0, 0x300, 4):
        m.poke(E + a, 0)
    for a in range(0, 0x40, 4):
        m.poke(REC + a, 0)
        m.poke(HDR + a, 0)
    for b in (BANK_P, BANK_T):
        for a in range(0, 0x100, 4):
            m.poke(b + a, 0)
    for (bank, word, val) in (banks or []):
        m.poke(bank + 4 * word, val)


def diff_ram(before, after, ignore=((0x801FFE00, 0x80200000), (E, E + 0x300), (0x80098708, 0x8009870C))):
    out = []
    n = len(before)
    for base in range(0, n, 4096):
        if before[base:base + 4096] != after[base:base + 4096]:
            for i in range(base, base + 4096):
                if before[i] != after[i]:
                    a = 0x80000000 + i
                    if any(lo <= a < hi for lo, hi in ignore):
                        continue
                    out.append(a)
    return out


def one(label, rec, hdr_contents, seed, rec_code=0, flag=0, banks=None, junk40=0xA5A5A5A5, junk3c=0xA5A5A5A5):
    """rec = True/False (record pointer present)"""
    reset(banks)
    m.poke(E + 0x64, HDR)
    m.poke(HDR + 0x1C, hdr_contents << 24)          # byte +0x1F is the high byte of the word at +0x1C (little endian)
    if rec:
        m.poke(E + 0x44, REC)
        m.poke(REC + 0x10, (flag & 0xFFFF) | (rec_code << 16))   # hu +0x10 = flag, byte +0x12 = code
    m.poke(E + 0x3C, junk3c)
    m.poke(E + 0x40, junk40)
    m.poke(RNG, seed)
    before = bytes(m.ram)
    m.call(0x80032A40, E)
    after = bytes(m.ram)
    item, fl, st = s32(m.peek(E + 0x3C)), s32(m.peek(E + 0x40)), m.peek(RNG)
    n = 0
    s = seed
    while s != st and n < 4:
        s = lcg(s)
        n += 1
    assert s == st, (label, seed, st)
    others = diff_ram(before, after)
    eo = E & 0x1FFFFFFF
    ent_changed = sorted({a - eo - (a - eo) % 4 for a in range(eo, eo + 0x300) if before[a] != after[a]})
    assert set(ent_changed) <= {0x3C, 0x40}, ent_changed
    return dict(label=label, rec=int(rec), header_contents=hdr_contents, rec_code=rec_code if rec else None, flag=flag if rec else None,
                banks=[list(b) for b in (banks or [])], rng_in=seed, item=item, flag_out=fl, rng_out=st, draws=n,
                other_writes=['%08X' % a for a in others])


def find_seed(n1, n2=None):
    """a seed whose first draw has top nibble n1 (and second n2)"""
    g = 0x2468ACE1
    while True:
        g = lcg(g)
        s1 = lcg(g)
        if (s1 >> 28) == n1 and (n2 is None or (lcg(s1) >> 28) == n2):
            return g


S0 = 0x12345678
V = []
# R1: record-less (SpawnWarpEntity, hero, SpawnChild): code = header +0x1F ; +0x40 always written 0
for c in (0x00, 0x01, 0x24, 0x61, 0x62, 0x7F, 0x80, 0x82, 0xFB, 0xFC, 0xFF):
    for nib in ((None,) if c < 0x80 else (3, 14)):
        seed = S0 if nib is None else find_seed(nib)
        V.append(one('R1 record-less header=0x%02X nib=%s' % (c, nib), False, c, seed))
# R2: record present, flag 0: rec code overrides header only if nonzero
for rc, hc in ((0x00, 0x00), (0x00, 0x82), (0x24, 0x82), (0x82, 0x00), (0x82, 0x24), (0x61, 0xFF), (0x62, 0xFF)):
    V.append(one('R2 record flag=0 rec_code=0x%02X header=0x%02X' % (rc, hc), True, hc, find_seed(3) if (rc | hc) >= 0x80 else S0, rec_code=rc, flag=0))
# R3: flag != 0 and the bit NOT set: stored in +0x40 after the 0x800 threshold ; low 15 bits >= 0x800 -> flag 0 (then it behaves as flag 0)
for fl in (1, 5, 31, 32, 33, 0x7FF, 0x800, 0x801, 0x7FFF, 0x8001, 0x801F, 0x87FF, 0x8800, 0xFFFF):
    V.append(one('R3 record flag=0x%04X bit not set, rec_code=0x82 header=0x00' % fl, True, 0, find_seed(3), rec_code=0x82, flag=fl))
# R4: flag != 0 and the bit SET in the right bank -> code 0, no draw
for fl, banks in ((1, [(BANK_P, 0, 0x2)]), (5, [(BANK_P, 0, 0x20)]), (32, [(BANK_P, 1, 0x1)]), (33, [(BANK_P, 1, 0x2)]), (0x7FF, [(BANK_P, 63, 0x80000000)]),
                  (0x8001, [(BANK_T, 0, 0x2)]), (0x87FF, [(BANK_T, 63, 0x80000000)]), (0x8020, [(BANK_T, 1, 0x1)])):
    V.append(one('R4 flag=0x%04X bit SET (right bank), rec_code=0x82' % fl, True, 0x82, find_seed(3), rec_code=0x82, flag=fl, banks=banks))
# R5: bit set in the WRONG bank counts as not set (flag bit 0x8000 selects the bank)
for fl, banks in ((1, [(BANK_T, 0, 0x2)]), (0x8001, [(BANK_P, 0, 0x2)]), (0x8020, [(BANK_P, 1, 0x1)]), (5, [(BANK_P, 0, 0x2)]), (5, [(BANK_P, 1, 0x20)])):
    V.append(one('R5 flag=0x%04X bit set in the wrong bank / other bit' % fl, True, 0x82, find_seed(3), rec_code=0x82, flag=fl, banks=banks))
# R6: flag set but record code 0 and header nonzero (flag set kills the code whatever its source)
V.append(one('R6 flag=1 bit SET, rec_code=0, header=0xFC', True, 0xFC, find_seed(12), rec_code=0, flag=1, banks=[(BANK_P, 0, 0x2)]))
V.append(one('R6b flag=1 bit not set, rec_code=0, header=0xFC (header used)', True, 0xFC, find_seed(12), rec_code=0, flag=1))

json.dump(V, open('out/roll_vectors.json', 'w'), indent=0)
bad_other = [v for v in V if v['other_writes']]
print('vectors', len(V), 'with writes outside the entity (+0x3C/+0x40) and the RNG word:', len(bad_other))
for v in bad_other:
    print(' ', v['label'], v['other_writes'])
for v in V:
    print('%-70s rng_in %08X item %3d flag_out 0x%04X draws %d rng_out %08X' % (v['label'], v['rng_in'], v['item'], v['flag_out'] & 0xFFFFFFFF, v['draws'], v['rng_out']))
