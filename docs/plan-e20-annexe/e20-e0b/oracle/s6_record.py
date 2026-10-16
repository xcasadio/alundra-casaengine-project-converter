"""s6_record.py - the loot roll through the REAL record spawner 0x8003A1B8 -> InitializeEntity 0x80039D04 -> 0x80032A40 (the record path), using e14world.setup (flat field, Beannoide
sprite 79 of the map page whose header Contents is 0xFC).  One record per machine; the flag banks are poked before the spawn.  Output: out/record_vectors.json."""
import sys
import json
import copy
sys.path.insert(0, 'tools')
sys.path.insert(0, '.')
import e14world as E
from machine import M32, s32

BANK_P, BANK_T = 0x801EB344, 0x801EBA40
TAB = 0x80028E0C


def lcg(s):
    return (s * 0x7D2B89DD + 0xE06A02E7) & M32


def find_seed(n1):
    g = 0x2468ACE1
    while True:
        g = lcg(g)
        if (lcg(g) >> 28) == n1:
            return g


def model_item(code, s, rows):
    n = 0
    while True:
        if code & 0x80:
            s = lcg(s)
            n += 1
            code = rows[code & 0x7F][s >> 28]
        else:
            return (code if code < 0x62 else 0), s, n


cases = [
    # label, Contents(2 bytes), _10 flag, banks [(bank, word, value)], seed-nibble
    ('rec code 0, flag 0 -> header 0xFC', 0x0000, 0x0000, [], 3),
    ('rec code 0, flag 0 -> header 0xFC (deep)', 0x0000, 0x0000, [], 15),
    ('rec code 0x82 overrides header 0xFC', 0x0082, 0x0000, [], 3),
    ('rec code 0x24 direct item, no draw', 0x0024, 0x0000, [], 3),
    ('rec Contents 0x0100: low byte 0 -> header 0xFC, high byte ignored', 0x0100, 0x0000, [], 15),
    ('rec Contents 0x0182: high byte ignored, code 0x82', 0x0182, 0x0000, [], 3),
    ('flag 5, bit clear (persistent bank): rolled, +0x40 = 5', 0x0082, 0x0005, [], 3),
    ('flag 5, bit set (persistent bank): code 0, no draw, +0x40 = 5', 0x0082, 0x0005, [(BANK_P, 0, 0x20)], 3),
    ('flag 0x8005, bit set (temporary bank): code 0, no draw', 0x0082, 0x8005, [(BANK_T, 0, 0x20)], 3),
    ('flag 0x8005, bit set only in the persistent bank: rolled', 0x0082, 0x8005, [(BANK_P, 0, 0x20)], 3),
    ('flag 0x0800 (>= threshold): cleared to 0 and rolled', 0x0082, 0x0800, [], 3),
    ('flag 5 bit set, rec code 0 header 0xFC: code 0, no draw (flag kills header code too)', 0x0000, 0x0005, [(BANK_P, 0, 0x20)], 15),
]

out = []
rows = None
orig_spawn = E.spawn
for label, contents, flag, banks, nib in cases:
    seed = find_seed(nib)

    def spawn2(m, kind, recs, banks=banks):
        for (bank, word, val) in banks:
            m.poke(bank + 4 * word, val)
        return orig_spawn(m, kind, recs)
    E.spawn = spawn2
    tmpl = copy.deepcopy(E.record_template('beannoide', 0))
    tmpl['Contents'] = contents
    tmpl['_10'] = flag
    spec = dict(map=None, kind='beannoide', recs=[dict(x=20, y=11, h=0, template=tmpl)], hero_px=(452, 96), seed=seed, direction=24)
    m, meta = E.setup(spec)
    if rows is None:
        def rb(a):
            return m.peek(a & ~3) >> (8 * (a & 3)) & 0xFF
        rows = [[rb(TAB + 16 * r + c) for c in range(16)] for r in range(128)]
    sp = meta['spawn'][0]
    # model: effective flag, bank test, code
    eff_flag = 0 if (flag & 0x7FFF) >= 0x800 else flag
    code = (contents & 0xFF) or 0xFC
    if eff_flag:
        bank_set = any(((b == (BANK_T if eff_flag & 0x8000 else BANK_P)) and (w == (eff_flag >> 5) & 0x7F) and (v >> (eff_flag & 31)) & 1) for (b, w, v) in banks)
        if bank_set:
            code = 0
    want_item, want_st, want_n = model_item(code, seed, rows)
    ok = sp['contents_item'] == want_item and sp['rng_after'] == want_st and sp['contents_flag'] == eff_flag and sp['rng_before'] == seed
    out.append(dict(label=label, rec_Contents=contents, rec_10=flag, banks=[[hex(b), w, hex(v)] for b, w, v in banks], rng_in=sp['rng_before'], item=sp['contents_item'],
                    flag40=sp['contents_flag'], rng_out=sp['rng_after'], draws=want_n, model_ok=ok))
    print('%-92s rng_in %08X item %3d +0x40=0x%04X draws %d rng_out %08X  model %s' % (label, sp['rng_before'], sp['contents_item'], sp['contents_flag'] & 0xFFFF, want_n, sp['rng_after'], 'OK' if ok else 'MISMATCH'))
E.spawn = orig_spawn
json.dump(out, open('out/record_vectors.json', 'w'), indent=0)
print('all model_ok:', all(o['model_ok'] for o in out))
