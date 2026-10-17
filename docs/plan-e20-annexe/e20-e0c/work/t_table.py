import exelib as e, collections, json
T = 0x80028E0C
def row(r): return [e.u8(T + r * 16 + i) for i in range(16)]
rows = {r: row(r) for r in range(128)}
def dist(code, depth=0):
    """exact distribution (item -> prob numerator over 16^k) and expected draws; returns (Counter item->fraction, expected draws)"""
    from fractions import Fraction
    if code >= 0x100: return {0: Fraction(1)}, Fraction(0)
    if not (code & 0x80):
        return {(code if code < 0x62 else 0): Fraction(1)}, Fraction(0)
    out = collections.defaultdict(Fraction); ed = Fraction(1)
    for c in rows[code & 0x7F]:
        d, dd = dist(c, depth + 1)
        for k, v in d.items(): out[k] += v / 16
        ed += dd / 16
    return dict(out), ed
# which rows are referenced as targets (>=0x80) and which codes (0x80..0xFF) are used as Contents in the data is done in the census
print('rows non-trivial (any entry nonzero):')
res = {}
for r in range(128):
    code = 0x80 | r
    d, ed = dist(code)
    res[code] = (d, ed)
    if any(rows[r]):
        print(f'{code:#04x}', 'row', ' '.join(f'{x:02x}' for x in rows[r]), '=>', {hex(k): str(v) for k, v in sorted(d.items())}, 'E[draws]=', ed)
# check for cycles / invalid entries
inv = [(r, i, rows[r][i]) for r in range(128) for i in range(16) if 0x62 <= rows[r][i] < 0x80]
print('entries in 0x62..0x7F (returned as 0):', inv)
