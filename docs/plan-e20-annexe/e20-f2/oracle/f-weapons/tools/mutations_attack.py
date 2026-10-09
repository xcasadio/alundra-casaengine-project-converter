"""mutations_attack.py - E20.b: can the attack oracle be wrong without anyone noticing?  Each mutation changes ONE thing (a pass left out, a byte or an instruction word
of the emulated binary, a field, the field map, a start value) and AT1 / AT7 / AT9 are run again.  The expectation of each mutation is written BEFORE the run, in this file:
  'differs' = the trace of the scenario must change (the oracle really depends on that piece of the binary / state),
  'same'    = the trace must not change (a control: the oracle does not depend on it).
A mutation whose result does not match its expectation makes the script exit 1.   writes outdir/mutations.txt
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import attack as A
import world as W

SQ = A.SQUARE
FLAG_ALL = lambda col, row: (0, 0, 0, 0x20)


def sig(res):
    return tuple((r['anim_target'], r['anim_cur'], r['force_x'], r['force_z'], r['pos_x'] - res['pre']['pos_x'], r['pos_z'], r['rec_ptr'], r['rec_cnt'], r['charge_timer'],
                  tuple(r['events'])) for r in res['rows'])


def short(res):
    ch = []
    prev = res['pre']['anim_cur']
    for r in res['rows']:
        if r['anim_cur'] != prev:
            ch.append('t%d:%02X' % (r['tick'], r['anim_cur']))
        prev = r['anim_cur']
    sw = [r['tick'] for r in res['rows'] if r['rec_ptr'] != 0]
    return 'anim_cur changes %s, record pointer set on %s' % (' '.join(ch) or 'none', sw or 'never')


def patch8(addr, val):
    return lambda m: m.poke8(addr, val)


def patch32(addr, val):
    return lambda m: m.poke(addr, val)


def seq(*fs):
    def f(m):
        for g in fs:
            g(m)
    return f


def set_ent(off, val):
    return lambda m: m.poke(W.ent(0) + off, val)


# (label, scenario base, spec changes, expectation, why)
MUTS = [
    ('skip the events pass 0x800386D0 (MovePlayer)', 'AT1', dict(skip=(0x800386D0,)), 'differs', 'no MovePlayer, no press'),
    ('skip the animation pass 0x80038E18', 'AT1', dict(skip=(0x80038E18,)), 'differs', 'the swing never starts on screen nor chains back'),
    ('skip the physics pass 0x80038364', 'AT1', dict(skip=(0x80038364,)), 'same', 'the Idle hero is still on the ground: the swing is the same (control)'),
    ('hero +0x20 = 1 (early exit of MovePlayer)', 'AT1', dict(mutate=set_ent(0x20, 1)), 'differs', 'MovePlayer leaves before the state switch'),
    ('binary byte 0x80028C79 (ground animation of weapon record 0) 0x10 -> 0x11', 'AT1', dict(mutate=patch8(0x80028C79, 0x11)), 'differs', 'the swing animation comes from the binary table'),
    ('binary byte 0x80028C15 (item 1 -> weapon record) 0 -> 1', 'AT1', dict(mutate=patch8(0x80028C15, 1)), 'differs', 'the basic sword would use record 1 (Steel Edge swing 0x3F)'),
    ('binary byte 0x80028C79+13*1 (record 1 ground anim) 0x3F -> 0x55, no effect on the basic sword', 'AT1', dict(mutate=patch8(0x80028C79 + 13, 0x55)), 'same',
     'control: another record is not read by the basic sword'),
    ('PlayerTryAttack gate: nop the bnez at 0x8002EB28 on the gated field', 'AT7', dict(mutate=patch32(0x8002EB28, 0)), 'differs', 'with the branch removed the 0x2000 gate no longer blocks'),
    ('field without the 0x2000 bit instead of AT7 field', 'AT7', dict(cells=lambda col, row: (0, 0, 0, 0)), 'differs', 'AT7 is blocked only by the ground-property bit'),
    ('WeaponId 2 (bow slot) but the bow not owned', 'AT1', dict(mutate=lambda m: m.call(0x8004E484, 2)), 'same', 'GetItemIdFromSlotId finds no owned item: no weapon -> record 0 (same swing as the basic sword)'),
    ('Steel Edge owned and equipped (AT9 item 2)', 'AT1', dict(item=2), 'differs', 'weapon record 1 gives the 0x3F swing'),
    ('random state word 0x80098708 changed', 'AT1', dict(seed=0xDEADBEEF), 'same', 'control: no draw happens in a sword swing'),
    ('hero start (140, 60) px instead of (100, 100)', 'AT1', dict(start=(140, 60)), 'same', 'control: the swing does not depend on the position on a flat field'),
    ('one-tick tap replaced by Square held 1..5 (no new edge after tick 1)', 'AT1', dict(holds={t: SQ for t in range(1, 6)}), 'same', 'control: only the edge counts'),
    ('no Square at all', 'AT1', dict(holds={}), 'differs', 'no press, no swing'),
]


def build(base, ch):
    if base == 'AT1':
        spec = dict(name='AT1', holds={1: SQ}, n=60)
    else:
        spec = dict(name='AT7', holds={1: SQ}, n=30, cells=FLAG_ALL)
    kw = {}
    for k, v in ch.items():
        if k == 'seed':
            kw['seed'] = v
        else:
            spec[k] = v
    return spec, kw


def main(outdir):
    base = {}
    for b in ('AT1', 'AT7'):
        spec, _ = build(b, {})
        base[b] = A.run(spec)
    out = ['# E20.b mutation tests: each line = mutation | base scenario | expectation (written in advance) | result | verdict',
           '# baseline AT1: %s' % short(base['AT1']), '# baseline AT7: %s' % short(base['AT7'])]
    bad = 0
    for label, b, ch, want, why in MUTS:
        spec, kw = build(b, ch)
        res = A.run(spec, **kw)
        got = 'differs' if sig(res) != sig(base[b]) else 'same'
        ok = got == want
        bad += 0 if ok else 1
        out.append('%-95s | %s | expect %-7s | got %-7s | %s | %s | %s' % (label, b, want, got, 'OK' if ok else 'MISMATCH', why, short(res)))
    out.append('VERDICT: %s' % ('all %d mutations behave as expected' % len(MUTS) if not bad else '%d MISMATCH' % bad))
    open(os.path.join(outdir, 'mutations.txt'), 'w', encoding='utf-8').write('\n'.join(out) + '\n')
    print('\n'.join(out[-4:]))
    return 1 if bad else 0


if __name__ == '__main__':
    sys.exit(main(sys.argv[1] if len(sys.argv) > 1 else os.path.join(HERE, '..', 'out')))
