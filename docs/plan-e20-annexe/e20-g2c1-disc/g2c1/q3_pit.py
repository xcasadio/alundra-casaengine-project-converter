"""q3_pit.py - O-G-2 / Q-G2C-3 in the oracle: the pick phase 0x800386D0 destroys an entity that has DestroyOnVramFlags (0x200000) when +0x180 & 0x8004 (the OR of the four corner words that qualify, 0x80038064).
REAL code. Harness writes: pad words, cell field, object placement. Thrown east from (109,100) at T=61 over cells of columns 6 to 9 whose word is varied.
Also: the on-ground blocked branch of C4 with a 0x1001 corner (status 3) : a 0x1001 band (columns 6..9) with a wall (wk=0x40) in column 8.
Run: python -B -I -X utf8 q3_pit.py"""
import os, sys
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import c4_scen as S
import gl, ana


def run(label, name, cells, hold=gl.RIGHT, n=150):
    m, c, rows = S.throw_run(name, cells=cells, throw_hold=hold, n=n)
    chg = ana.changes(rows, c, ['status', 'anim_t', 'flags6C', 'dir_t', 'bounced', 'slope18C', 'p184'], first=60)[:10]
    fx = [(r['tick'], x[:70]) for r in rows for x in r['journal'] if x.startswith('fx')][:3]
    dead = ana.first_tick(rows, lambda r: r['c'][c]['status'] == 0, first=62)
    last = rows[-1]['c'][c]
    print('%-46s %-10s: dead t%s ; fx %s ; changes %s ; final st %d anim %d pos %.1f' % (label, name, dead, fx, chg, last['status'], last['anim_t'], last['px'] / 65536))


cols = lambda lo, hi, **kw: (lambda cc, rr: dict(**kw) if lo <= cc <= hi else None)
for name in ('tonneau34', 'tronc117', 'etoile107'):
    run('walkability 0x04 (hole bit) cols 6-9', name, cols(6, 9, wk=0x04))
    run('ground property 0x80 (portal floor) cols 6-9', name, cols(6, 9, gp=0x80))
for name in ('tonneau34', 'tronc117', 'etoile107'):
    run('0x1001 band cols 6-9 + wall wk 0x40 col 8', name, lambda cc, rr: (dict(wk=0x41, gp=0x10) if cc == 8 else dict(wk=0x01, gp=0x10)) if 6 <= cc <= 9 else None)
    run('0x1001 band cols 6-9 + wall 0x1041 col 9', name, lambda cc, rr: (dict(wk=0x41, gp=0x10) if cc == 9 else dict(wk=0x01, gp=0x10)) if 6 <= cc <= 9 else None)
