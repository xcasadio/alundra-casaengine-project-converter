"""wave_hit.py - bin agent of the E20.f3 discovery.  The sword wave (steel, type 27) against a real Beannoid (map 14), east and north, plus a reach sweep and the fire / ice ball hit effects.
Real code on the f-projectiles machine (pworld.py).  usage: python -B wave_hit.py <east|north|sweep|balls|grass>
"""
import sys, os
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import pworld as P
import world as W
import c1world as C
from wave_run import run_wave, DIRS, RELEASE

OUT = os.path.join(HERE, '..', 'out')


def hits_of(res, tslot, by=None):
    out = []
    for r in res['rows']:
        t = r['slots'].get(tslot)
        if t and t['c1d0'] == 25 and (by is None or t['touch_slot'] == by):
            out.append(r['tick'])
    return out


def first_hit(sword, direction, xpos, ypos):
    res = run_wave('x', sword=sword, direction=direction, n=150, targets=[(xpos, ypos, 0)])
    h = [(r['tick'], r['slots'][1]['touch_slot']) for r in res['rows'] if r['slots'].get(1) and r['slots'][1]['c1d0'] == 25]   # (tick, TouchingEntity slot): 0 = hero's own swing, 2 = the wave
    return h, res


def show(name, res, tslot, wslot, first=114, last=146):
    lines = ['# %s' % name]
    for r in res['rows']:
        if r['tick'] < first or r['tick'] > last:
            continue
        t = r['slots'].get(tslot)
        w = r['slots'].get(wslot)
        ev = [j for j in r['journal'] if not j.startswith('stub')]
        if t is None:
            continue
        flag = ''
        if t['c1d0'] == 25:
            flag = 'HIT'
        if flag or ev or (w and w['cnt1c'] in (1, 2)):
            lines.append('t%03d target pos=(%.1f,%.1f) c1d0=%d touch=%s hitc(target)=%d | wave box=(%.0f,%.0f) c1c=%s hitc=%s | %s %s' % (
                r['tick'], t['px'] / 65536, t['py'] / 65536, t['c1d0'], t['touch_slot'], t['hitc'], (w['hbx'] / 65536) if w else -1, (w['hby'] / 65536) if w else -1,
                w['cnt1c'] if w else None, w['hitc'] if w else None, flag, ' '.join(ev)))
    return '\n'.join(lines)


if __name__ == '__main__':
    which = sys.argv[1]
    os.makedirs(OUT, exist_ok=True)
    if which in ('east', 'north'):
        xp, yp = (12, 12) if which == 'east' else (7, 6)
        h, res = first_hit('steel', which, xp, yp)
        txt = show('wave %s vs Beannoid at map-record (%d,%d) = px (%d,%d), steel' % (which, xp, yp, 12 * xp + 12, 8 * yp + 8), res, 1, 2)
        txt += '\n# hit ticks: %s (spawn tick S=117)\n' % h
        open(os.path.join(OUT, 'wave_hit_%s.txt' % which), 'w').write(txt)
        print(txt)
    if which == 'sweep':
        lines = ['# reach sweep: first hit tick of the steel wave on a Beannoid (map 14, body offset (-10,-7,0) 20x14x8), hero at (100,100); S=117']
        for direction, specs in (('east', [(xp, 11) for xp in range(6, 20)]), ('north', [(7, yp) for yp in range(0, 12)]), ('west', [(xp, 11) for xp in range(0, 8)]), ('south', [(7, yp) for yp in range(11, 22)])):
            for xp, yp in specs:
                px_, py_ = 12 * xp + 12, 8 * yp + 8
                h, res = first_hit('steel', direction, xp, yp)
                lines.append('%-5s target px=(%3d,%3d) rel=(%+4d,%+4d) hits (tick,attacker slot)=%s' % (direction, px_, py_, px_ - 100, py_ - 100, [(t, 'wave' if s_ == 2 else 'hero') for t, s_ in h if t > 100]))
                sys.stdout.flush()
        open(os.path.join(OUT, 'wave_reach_sweep.txt'), 'w').write('\n'.join(lines) + '\n')
        print('\n'.join(lines))
    if which == 'balls':
        lines = []
        for name, item in (('fire', 16), ('ice', 14)):
            res = P.run(dict(item=item, n=110, holds={1: W.SQUARE}, after=0, targets=[(12, 12, 0)]))
            lines.append('===== %s rod, Square at t1, target Beannoid at px (156,104)' % name)
            for r in res['rows']:
                t = r['slots'].get(1)
                a = r['slots'].get(2)
                ev = [j for j in r['journal'] if not j.startswith('stub')]
                evf = [e for e in ev if e.startswith('fxattach') or e.startswith('fx(id=9')]
                if t and (t['c1d0'] == 25 or evf):
                    lines.append('t%03d target c1d0=%d touch=%s | ball anim_t=%s hitc=%s px=%.1f pz=%.2f | rng=%08X | %s' % (
                        r['tick'], t['c1d0'], t['touch_slot'], a and a['anim_t'], a and a['hitc'], a['px'] / 65536 if a else 0, a['pz'] / 65536 if a else 0, r['rng'] & 0xFFFFFFFF, ' '.join(evf)))
            hits = [r['tick'] for r in res['rows'] if r['slots'].get(1) and r['slots'][1]['c1d0'] == 25]
            lines.append('hit ticks (target +0x1D0 = 25): %s' % hits)
        open(os.path.join(OUT, 'balls_vs_target.txt'), 'w').write('\n'.join(lines) + '\n')
        print('\n'.join(lines))
