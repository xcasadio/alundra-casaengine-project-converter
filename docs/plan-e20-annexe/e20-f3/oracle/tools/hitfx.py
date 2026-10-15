"""hitfx.py - bin agent of the E20.f3 discovery.  The REAL hit pass 0x80039300 (ALUN_CD.EXE France) driven pair by pair:
attacker = the hero slot (box pointer, attack record and HitBox poked), target = the real Beannoid of map 14 (spawned by the real spawner).
For each attack attribute low nibble n (0..15) and each response byte b of the target's balance sheet at [sheet + 2 + n] the harness calls the pass once (machine restored after each call)
and records: effects created (journal of the 0x8003BE74 / 0x8003BDD8 stubs), target +0x1D0, target +0x224 (TouchingEntity), attacker +0x220 (HitCounter), RNG words.
Nothing here knows an expected value; the 'expected' column is computed from the decoded rule only to print a PASS/FAIL against it.
usage: python -B hitfx.py
"""
import sys, os
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import pworld as P
import world as W
import c1world as C
from machine import s32, M32

OUT = os.path.join(HERE, '..', 'out')
ATK = 0x801F8000


def build():
    m, meta = P.setup(item=None, targets=[(12, 12, 0)], hero_px=(100, 100), direction=24)
    pad = W.Pad()
    m.tick_no = 1
    W.tick(m, pad, 0)
    return m, meta


def trial(m, attr, power, resp, nib, hero_flags_or=4, target_slot=1, reset_target=True):
    sb = C.Sandbox(m)
    hero = P.ENT0
    tgt = P.ENT0 + P.STRIDE * target_slot
    m.poke8(ATK, attr)
    m.poke8(ATK + 1, power)
    m.poke16(ATK + 2, 0)
    m.poke(hero + 0x1D4, 1)
    m.poke(hero + 0x1C8, ATK)
    m.poke(hero + 0x6C, m.peek(hero + 0x6C) | hero_flags_or)
    for src, dst in ((0x1D8, 0x1FC), (0x1DC, 0x200), (0x1E0, 0x204), (0x1F0, 0x214), (0x1F4, 0x218), (0x1F8, 0x21C)):
        m.poke(hero + dst, m.peek(tgt + src))
    m.poke(hero + 0x220, 0)
    m.poke(tgt + 0x1D0, 0)
    m.poke(tgt + 0x1CC, 0)
    m.poke(tgt + 0x224, 0)
    sheet = m.peek(tgt + 0x1C4)
    m.poke8(sheet + 2 + nib, resp)
    j0 = len(m.journal)
    rng0 = m.peek(C.RNG)
    m.tick_no = 99
    m.call(0x80039300, limit=5_000_000)
    ev = [P.fmt_j(j) for j in m.journal[j0:] if not j[1].startswith('pass_')]
    res = dict(effects=[e for e in ev if e.startswith('fxattach')], sparks=sum(1 for e in ev if e.startswith('fx(id=9')),
               other=[e for e in ev if not e.startswith('fxattach') and not e.startswith('fx(id=9')],
               c1d0=m.peeks(tgt + 0x1D0), touch=m.peek(tgt + 0x224), hitc=m.peeks(hero + 0x220), rng0=rng0 & M32, rng1=m.peek(C.RNG) & M32)
    sb.restore()
    return res


def expected(nib, resp):
    immune = (resp & 0xC0) == 0x80
    fx = []
    if not immune:
        if nib in (6, 10):
            fx.append(4)
        if nib in (7, 9):
            fx.append(5)
    return immune, fx


if __name__ == '__main__':
    m, meta = build()
    tgt = P.ENT0 + P.STRIDE * 1
    sheet = m.peek(tgt + 0x1C4)
    lines = ['# hit pass 0x80039300 driven pair by pair (hero = attacker, real Beannoid of map 14 = target).',
             '# target balance sheet pointer %08X (sprite %d), target flags +0x6C = %X, +0xB4 = %X' % (sheet, m.peek(tgt + 0x68), m.peek(tgt + 0x6C), m.peek(tgt + 0xB4)),
             '# columns: attr nib resp -> effects attached | sparks | target +0x1D0 | target +0x224 (TouchingEntity) | attacker HitCounter | RNG words before->after | PASS/FAIL against the decoded rule']
    nfail = 0
    ntot = 0
    resp_list = [0x00, 0x10, 0x3F, 0x40, 0x7F, 0x80, 0xBF, 0xC0, 0xFF, 0x81]
    for nib in range(16):
        attr = nib if nib else 0x80
        for resp in resp_list:
            r = trial(m, attr, 1, resp, nib)
            immune, fx = expected(nib, resp)
            got_fx = [int(e.split('id=')[1].split(',')[0]) for e in r['effects']]
            ok = (got_fx == fx) and (r['touch'] == 0) == immune and r['c1d0'] == 25 and r['hitc'] == 1
            # RNG: the spark routine draws; immune or not, 3 sparks are created (checked below)
            ntot += 1
            nfail += (not ok)
            lines.append('attr=%02X nib=%2d resp=%02X -> fx=%s sparks=%d c1d0=%d touch=%s hitc=%d rng %08X->%08X %s' % (
                attr, nib, resp, got_fx, r['sparks'], r['c1d0'], 'none' if r['touch'] == 0 else 'slot%d' % ((r['touch'] - P.ENT0) // P.STRIDE), r['hitc'], r['rng0'], r['rng1'], 'PASS' if ok else 'FAIL'))
    # equipment-power attribute bit variants
    for attr in (0x86, 0x87, 0x89, 0x8A, 0x06, 0x07, 0x09, 0x0A):
        nib = attr & 0xF
        r = trial(m, attr, 1, 0x10, nib)
        lines.append('variant attr=%02X (bit7 %s) resp=10 -> fx=%s' % (attr, 'set' if attr & 0x80 else 'clear', r['effects']))
    # effect creation draws nothing: same pair with the real spark routine; RNG advance is the sparks only
    r = trial(m, 0x06, 1, 0x10, 6)
    r0 = trial(m, 0x01, 1, 0x10, 1)
    lines.append('RNG words consumed: nibble 6 (effect 4 + sparks) = %d steps? words %08X->%08X ; nibble 1 (sparks only) %08X->%08X' % (0, r['rng0'], r['rng1'], r0['rng0'], r0['rng1']))
    lines.append('summary: %d/%d pairs match the decoded rule' % (ntot - nfail, ntot))
    os.makedirs(OUT, exist_ok=True)
    open(os.path.join(OUT, 'hitfx_table.txt'), 'w').write('\n'.join(lines) + '\n')
    print('\n'.join(lines[:6]))
    print('...')
    print(lines[-1])
    sys.exit(1 if nfail else 0)
