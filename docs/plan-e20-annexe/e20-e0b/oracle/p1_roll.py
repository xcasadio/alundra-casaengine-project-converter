"""p1_roll.py - CRITIC probe.  Independent re-check of the loot roll 0x80032A40 / 0x80032968 on the REAL code in the MIPS machine.
The model below was written by me from my own reading of the disassembly (critic/cdis.py 0x80032968 0x80032B28), NOT copied from the discovery reports.
Part 1: the documented vectors of the reports (section 4 B/C) re-run.
Part 2: fuzz with my own seeds, flags, codes, both banks, with and without a record.
Part 3: a full table dump (all 128 rows) read from the executable file and from machine RAM."""
import sys, os, json, random
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, 'tools'))
from machine import Machine, M32, s32
from cdis import D, off

RNG = 0x80098708
E, REC, HDR = 0x80180000, 0x80180400, 0x80180500
BANK_P, BANK_T = 0x801EB344, 0x801EBA40
TAB = 0x80028E0C


def step(s):
    return (s * 0x7D2B89DD + 0xE06A02E7) & M32


# my own table: read from the executable FILE, not from the machine
def fbyte(a):
    return D[off(a)]


ROWS = [[fbyte(TAB + 16 * r + c) for c in range(16)] for r in range(128)]


def choose(code, s):
    n = 0
    while True:
        if code >= 0x100:
            return 0, s, n
        if code & 0x80:
            s = step(s)
            n += 1
            code = ROWS[code & 0x7F][(s * 16) >> 32]
            continue
        return (code if code < 0x62 else 0), s, n


def roll(has_rec, flag, rec_code, hdr_code, s, banks):
    """banks = (persistent_words, temporary_words) dicts word-index -> value"""
    if not has_rec:
        f40 = 0
        code = hdr_code
    else:
        f = flag
        if (f & 0x7FFF) >= 0x800:
            f = 0
        f40 = f
        if f != 0:
            bank = banks[1] if (f & 0x8000) else banks[0]
            w = bank.get((f >> 3) & 0xFFC, 0)       # keyed by byte offset
            if (w >> (f & 31)) & 1:
                code = 0
                it, s2, n = choose(code, s)
                return it, f40, s2, n
        code = rec_code if rec_code else hdr_code
    it, s2, n = choose(code, s)
    return it, f40, s2, n


m = Machine()
m.watch_uninit = False


def machine_roll(has_rec, flag, rec_code, hdr_code, s, banks):
    for a in range(0, 0x300, 4):
        m.poke(E + a, 0)
    for a in range(0, 0x40, 4):
        m.poke(REC + a, 0)
        m.poke(HDR + a, 0)
    for b in (BANK_P, BANK_T):
        for a in range(0, 0x100, 4):
            m.poke(b + a, 0)
    for off_, v in banks[0].items():
        m.poke(BANK_P + off_, v)
    for off_, v in banks[1].items():
        m.poke(BANK_T + off_, v)
    m.poke(E + 0x64, HDR)
    m.poke(HDR + 0x1C, hdr_code << 24)
    if has_rec:
        m.poke(E + 0x44, REC)
        m.poke(REC + 0x10, (flag & 0xFFFF) | (rec_code << 16))
    m.poke(E + 0x3C, 0xA5A5A5A5)
    m.poke(E + 0x40, 0xA5A5A5A5)
    m.poke(RNG, s)
    m.call(0x80032A40, E)
    st = m.peek(RNG)
    n = 0
    t = s
    while t != st and n < 5:
        t = step(t)
        n += 1
    assert t == st, (hex(s), hex(st))
    return s32(m.peek(E + 0x3C)), s32(m.peek(E + 0x40)), st, n


bad = 0
total = 0

# ---- part 3: table
ram_rows = []
for r in range(128):
    row = []
    for c in range(16):
        a = TAB + 16 * r + c
        row.append(m.peek(a & ~3) >> (8 * (a & 3)) & 0xFF)
    ram_rows.append(row)
print('part3 table: machine RAM == executable file bytes:', ram_rows == ROWS)
nz = [r for r in range(128) if any(ROWS[r])]
print('non-zero rows (code 0x80|r):', ['0x%02X' % (0x80 | r) for r in nz])

# ---- part 1: documented vectors
def find_seed(n1):
    g = 0x2468ACE1
    while True:
        g = step(g)
        if (step(g) >> 28) == n1:
            return g

N3, N14, N15, S0 = find_seed(3), find_seed(14), find_seed(15), 0x12345678
print('seeds: N3=%08X N14=%08X N15=%08X' % (N3, N14, N15), '(reports: 4F1427FB 5EC362FE 985CF02A)')

docs = [
    # (label, has_rec, flag, rec_code, hdr_code, seed, banks, expected (item, f40, rng_out, draws) or None)
    ('rl hdr 0x82 @N3', 0, 0, 0, 0x82, 0x4F1427FB, ({}, {}), (85, 0, 0x345ED996, 1)),
    ('rl hdr 0xFC @S0', 0, 0, 0, 0xFC, S0, ({}, {}), (69, 0, 0x24D2C78A, 2)),
    ('rl hdr 0xF8 @S0', 0, 0, 0, 0xF8, S0, ({}, {}), (72, 0, 0xB806E07F, 1)),
    ('rl hdr 0xFC @N14', 0, 0, 0, 0xFC, 0x5EC362FE, ({}, {}), (84, 0, 0x15914CC0, 2)),
    ('flag1 clear, hdr 0xFC @9CAFDCAF', 1, 1, 0, 0xFC, 0x9CAFDCAF, ({}, {}), (71, 1, 0xD432A0B9, 2)),
    ('flag1 SET persistent, hdr 0xFC @9CAFDCAF', 1, 1, 0, 0xFC, 0x9CAFDCAF, ({0: 2}, {}), (0, 1, 0x9CAFDCAF, 0)),
    ('flag 0x8001 set in TEMP bank', 1, 0x8001, 0x82, 0x24, 0x4F1427FB, ({}, {0: 2}), (0, 0x8001, 0x4F1427FB, 0)),
    ('flag 0x8001 bit in PERSISTENT only (wrong bank)', 1, 0x8001, 0x82, 0x24, 0x4F1427FB, ({0: 2}, {}), (85, 0x8001, 0x345ED996, 1)),
    ('flag 0x0800 -> masked to 0', 1, 0x0800, 0x82, 0x24, 0x4F1427FB, ({}, {}), (85, 0, 0x345ED996, 1)),
    ('flag 0x07FF bit 31 of word 63 set', 1, 0x07FF, 0x82, 0x24, 0x4F1427FB, ({4 * 63: 0x80000000}, {}), (0, 0x07FF, 0x4F1427FB, 0)),
    ('rec code 0x24 direct, hdr 0x82', 1, 0, 0x24, 0x82, 0x4F1427FB, ({}, {}), (36, 0, 0x4F1427FB, 0)),
]
for label, hr, fl, rc, hc, s, bk, exp in docs:
    mm = machine_roll(hr, fl, rc, hc, s, bk)
    mo = roll(hr, fl, rc, hc, s, bk)
    mo_t = (mo[0], mo[1], mo[2], mo[3])
    total += 1
    ok_model = mm == mo_t
    ok_doc = (exp is None) or (mm == tuple(exp))
    if not (ok_model and ok_doc):
        bad += 1
    print('%-52s machine=%s model_ok=%s matches_report=%s' % (label, (mm[0], hex(mm[1]), hex(mm[2]), mm[3]), ok_model, ok_doc))

# ---- part 2: fuzz
rnd = random.Random(20261015)
FUZZ = 6000
fz_bad = 0
draw_hist = {0: 0, 1: 0, 2: 0}
DUMP = []
for k in range(FUZZ):
    hr = rnd.random() < 0.7
    flagpool = [0, 1, 5, 0x31, 0x7FF, 0x800, 0x8000, 0x8001, 0x8005, 0x87FF, 0xFFFF, 0x7FFF, 0x0123]
    fl = rnd.choice(flagpool) if rnd.random() < 0.6 else rnd.getrandbits(16)
    rc = rnd.choice([0, 0, 0x24, 0x62, 0x7F, 0x80, 0x81, 0x82, 0x83, 0xF6, 0xF7, 0xF8, 0xFA, 0xFB, 0xFC, 0xFD, 0xFE, 0xFF, rnd.getrandbits(8)])
    hc = rnd.choice([0, 0x24, 0x62, 0x80, 0x82, 0xF7, 0xF8, 0xFA, 0xFB, 0xFC, 0xFF, rnd.getrandbits(8)])
    s = rnd.getrandbits(32)
    pers = {}
    temp = {}
    # set the bit of the flag in one or both banks (or none) at random, plus noise
    for (d) in (pers, temp):
        for _ in range(rnd.randint(0, 3)):
            d[4 * rnd.randint(0, 63)] = rnd.getrandbits(32)
    if rnd.random() < 0.5 and (fl & 0x7FFF) < 0x800 and fl:
        tgt = temp if fl & 0x8000 else pers
        tgt[(fl >> 3) & 0xFFC] = tgt.get((fl >> 3) & 0xFFC, 0) | (1 << (fl & 31))
    bk = (pers, temp)
    mm = machine_roll(hr, fl, rc, hc, s, bk)
    mo = roll(hr, fl, rc, hc, s, bk)
    total += 1
    draw_hist[mm[3]] += 1
    DUMP.append([int(hr), fl, rc, hc, s, sorted(pers.items()), sorted(temp.items()), mm[0], mm[1], mm[2], mm[3]])
    if mm != mo:
        fz_bad += 1
        if fz_bad < 6:
            print('FUZZ MISMATCH', hr, hex(fl), hex(rc), hex(hc), hex(s), mm, mo)
print('part2 fuzz: %d vectors, %d mismatches, draws histogram %s' % (FUZZ, fz_bad, draw_hist))
bad += fz_bad
print('TOTAL vectors %d, failures %d' % (total, bad))
import json as _j
os.makedirs(os.path.join(HERE, 'out'), exist_ok=True)
_j.dump({'fields': ['has_record', 'flag', 'record_code', 'header_code', 'rng_in', 'persistent_words(byte_offset,value)', 'temporary_words(byte_offset,value)', 'item', 'flag_out(+0x40)', 'rng_out', 'draws'], 'vectors': DUMP}, open(os.path.join(HERE, 'out', 'roll_fuzz_vectors.json'), 'w'), separators=(',', ':'))
