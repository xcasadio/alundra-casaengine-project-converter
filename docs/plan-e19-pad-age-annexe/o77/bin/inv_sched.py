"""Schedules on the real binary code (inv_bin.py). Physical presses (one-sample taps unless said) at frames relative to P.
Reports, per schedule, the RenderScene frame (and so the sample it read = frame-1) of each inventory event, relative to P."""
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import inv_bin as ib

P = 5

def events(rows):
    ev = []
    prev = dict(flag6=0, sel6=0, flag4=0, sel4=0, ctrl=0, post=0, slot4=0, slot6=0)
    for r in rows:
        f = r['f'] - P
        for e in r['up_log']:
            ev.append(('UP', f, e[1] if e[0] == 'stub' else 'snd%d' % e[1]))
        rs = r['rs']
        for e in r['rs_log']:
            ev.append(('RS', f, e[1] if e[0] == 'stub' else 'snd%d' % e[1]))
        for k in ('flag6', 'sel6', 'flag4', 'sel4', 'ctrl', 'post', 'slot4', 'slot6'):
            if rs[k] != prev[k]:
                ev.append(('RS', f, '%s %d->%d' % (k, prev[k], rs[k])))
        prev = dict((k, rs[k]) for k in prev)
        up = r['up']
        if up['slot6'] != prev['slot6']:
            ev.append(('UP', f, 'slot6 %d->%d' % (prev['slot6'], up['slot6']))); prev['slot6'] = up['slot6']
        if up['ctrl'] != prev['ctrl']:
            ev.append(('UP', f, 'ctrl %d->%d' % (prev['ctrl'], up['ctrl']))); prev['ctrl'] = up['ctrl']
    return ev

def show(name, presses, frames=80):
    phys = lambda f: presses.get(f - P, 0)
    rows = ib.run(phys, P + frames)
    print('== %s  presses(rel P): %s' % (name, ', '.join('%+d:%s' % (k, hex(v)) for k, v in sorted(presses.items()))))
    for ph, f, e in events(rows):
        print('   %s frame P%+d (reads sample P%+d): %s' % (ph, f, f - 1 if ph == 'RS' else f, e))

if __name__ == '__main__':
    S = ib
    show('open only', {0: S.START}, 30)
    for q in (18, 19, 20):
        show('open + Down at P+%d' % q, {0: S.START, q: S.DOWN}, 30)
    show('open + Cross at P+19 (weapon slot 0)', {0: S.START, 19: S.CROSS}, 30)
    show('open + Start at P+19 (close)', {0: S.START, 19: S.START}, 60)
    show('open, close at P+19, Start again at P+38..', {0: S.START, 19: S.START, 38: S.START}, 70)
    show('open, close at P+19, Start again at P+39', {0: S.START, 19: S.START, 39: S.START}, 70)
    show('open, Triangle at P+19', {0: S.START, 19: S.TRIANGLE}, 60)
