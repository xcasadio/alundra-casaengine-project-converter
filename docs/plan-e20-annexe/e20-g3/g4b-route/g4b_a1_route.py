"""g4b_a1_route.py - E20.g4b T0 (a1): the route 'LIFT' of the binary, redone with the 12 records (rec9 included): from the hero's place after B1's walk (775.125, 216, z 64) to the portal 0 (cell (22, 23)):
down, left (the calage at the north wall of the door), contact with rec5 on the east face (x = 657), Square the tick after the contact (the lift), the gesture (36 ticks), carry west, the throw (Square) when x <= XT, then
as the route of the jump (rec9 stops the hero at 597, south, west, down).  A scripted player reads the machine before each tick; nothing is written after the start.
Usage: python -B -I g4b_a1_route.py [XT] [sweep]   -> out/g4b_a1_route.txt (XT default 600.0) or out/g4b_a1_sweep.txt"""
import os
import sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import multiprocessing as mp
import g4b_lib as L
G = L.G

YD = 288.0


def run_route(XT, ticks=520, trace=False):
    s = L.Sim((775, 216), 0, x_raw=int(775.125 * L.U))
    phase = 'down'
    log = []
    ev = {}
    sq_tick = None
    throw_tick = None
    for _ in range(ticks):
        h = s.hero()
        t = s.t + 1
        ph = phase
        hold = 0
        if phase == 'down' and h['y'] >= YD:
            phase = 'left'
        if phase == 'south' and h['y'] >= 343.5:
            phase = 'west'
        if phase == 'west' and h['x'] <= 541.0:
            phase = 'down2'
        if phase == 'after_throw' and h['x'] <= 600.0:
            phase = 'south'
        if phase == 'left':
            hold = G.LEFT
            if h['xcoll'] == 5:                       # the contact of the previous tick end: Square lifts (the first blocked tick would be the sword)
                hold = G.LEFT | G.SQUARE
                phase = 'gesture'
                ev['contact_state'] = (t, round(h['x'], 4), round(h['y'], 4))
        elif phase == 'gesture':
            hold = G.LEFT
            if h['carried'] == 5 and h['anim'] == 12:
                phase = 'carry'
        if phase == 'carry':
            hold = G.LEFT
            if h['x'] <= XT:
                hold = G.LEFT | G.SQUARE                 # the throw
                phase = 'after_throw'
                ev['throw'] = (t, round(h['x'], 4), round(h['y'], 4), round(h['z'], 4))
        elif phase == 'after_throw':
            hold = G.LEFT
        elif phase in ('down', 'south', 'down2'):
            hold = G.DOWN
        elif phase == 'west':
            hold = G.LEFT
        if phase == 'down2' and int(h['x'] // 24) == 22 and int(h['y'] // 16) == 23:
            phase = 'done'
            hold = 0
        if phase != ph:
            log.append((t, ph, phase, h['x'], h['y'], h['z'], h['xcoll'], h['carried']))
        if phase == 'gesture' and 'lift_tick' not in ev and ph == 'left':
            ev['lift_press'] = t
        s.tick(hold)
        c5 = s.crate(5)
        if 'lifted' not in ev and s.rows[-1]['carried'] == 5:
            ev['lifted'] = s.t
        if 'carry_stand' not in ev and s.rows[-1]['anim'] == 12:
            ev['carry_stand'] = s.t
        if 'thrown' not in ev and 'throw' in ev and s.rows[-1]['carried'] is None and s.t >= ev['throw'][0]:
            ev['thrown'] = s.t
        if 'thrown' in ev and 'break_anim' not in ev and c5['anim'] == 2:
            ev['break_anim'] = (s.t, round(c5['x'], 3), round(c5['y'], 3), round(c5['z'], 3))
        if 'thrown' in ev and 'gone' not in ev and c5['status'] == 0:
            ev['gone'] = s.t
        if 'portal' not in ev and int(s.rows[-1]['x'] // 24) == 22 and int(s.rows[-1]['y'] // 16) == 23:
            ev['portal'] = (s.t, round(s.rows[-1]['x'], 4), round(s.rows[-1]['y'], 4))
            break
    return s, log, ev


def summarize(XT):
    s, log, ev = run_route(XT)
    return XT, ev, log


if __name__ == '__main__':
    mp.freeze_support()
    args = sys.argv[1:]
    if args and args[-1] == 'sweep':
        xs = [654.0 - 2.0 * i for i in range(0, 32)]       # 654 .. 592
        with mp.Pool(16) as p:
            R = p.map(summarize, xs)
        out = ['E20.g4b T0 (a1) - sweep of the throw threshold XT (the throw is pressed at the first tick where the carried hero is at x <= XT); 12 records; start (775.125, 216, z 64)']
        for XT, ev, log in R:
            th = ev.get('throw')
            out.append('XT %6.1f : throw %s thrown_tick %s break %s gone %s portal %s' % (XT, th, ev.get('thrown'), ev.get('break_anim'), ev.get('gone'), ev.get('portal')))
        txt = '\n'.join(out) + '\n'
        open(os.path.join(G.OUT, 'g4b_a1_sweep.txt'), 'w', encoding='utf-8').write(txt)
        print(txt)
    else:
        XT = float(args[0]) if args else 600.0
        s, log, ev = run_route(XT)
        out = ['E20.g4b T0 (a1) - the LIFT route with the 12 records (binary, oracle machine); YD=%.1f XT=%.1f; start (775.125, 216, z 64)' % (YD, XT)]
        out.append('phases (a phase starts at the tick given, state read before the tick):')
        for e in log:
            out.append('  tick %3d : %-10s -> %-10s  hero x=%8.3f y=%8.3f z=%7.3f xcoll=%s carried=%s' % e)
        out.append('events: ' + repr(ev))
        out.append('trace: tick pad x y z anim ground xcoll carried support ; crate rec5 x y z anim status')
        for r in s.rows:
            c5 = s.crate(5)
            out.append(L.fmt_row(r))
        txt = '\n'.join(out) + '\n'
        open(os.path.join(G.OUT, 'g4b_a1_route.txt'), 'w', encoding='utf-8').write(txt)
        print('\n'.join(out[:14]))
