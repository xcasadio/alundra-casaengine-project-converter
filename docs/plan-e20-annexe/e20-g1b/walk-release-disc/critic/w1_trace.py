"""w1_trace.py - walk-release-disc W1: tick-by-tick state of the hero's physics fields in the REAL code of ALUN_CD.EXE (France), release of the walk.
Run from the copy of critic-scenarios:  python -B -I w1_trace.py [scenario ...]
Prints, per tick: pad, TargetAnim(+88) CurrentAnim(+90) TargetDir(+8C) CurDir(+94) AnimSetPtr(+9C) Speed(+F4) Accel(+F0) TargetForceX(+BC) StepX(+D4) ForceX(+C4) AdjX(+DC) FinalX(+E4) PosX(+114)
and the AnimationSet record (+8 speed u16, +D accel u8) the pointer designates.
"""
import sys, os
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.join(HERE, 'oracle'))
import gl
from machine import M32, s32

H = gl.HERO


def f(v):
    return '%.4f' % (v / 65536)


def snap(m, t, r):
    p = m.peek(H + 0x9C)
    sp = m.peek(p + 0x8) & 0xFFFF if p else -1
    ac = (m.peek(p + 0xC) >> 8) & 0xFF if p else -1       # byte 0xD
    r['w1'] = dict(
        ta=m.peek(H + 0x88), ca=m.peek(H + 0x90), td=m.peek(H + 0x8C), cd=m.peek(H + 0x94), setp=p, setspeed=sp, setacc=ac,
        speed=m.peeks(H + 0xF4), acc=m.peeks(H + 0xF0), tfx=m.peeks(H + 0xBC), tfy=m.peeks(H + 0xC0), stepx=m.peeks(H + 0xD4), stepy=m.peeks(H + 0xD8),
        fx=m.peeks(H + 0xC4), fy=m.peeks(H + 0xC8), adjx=m.peeks(H + 0xDC), adjy=m.peeks(H + 0xE0), finx=m.peeks(H + 0xE4), finy=m.peeks(H + 0xE8),
        px=m.peeks(H + 0x114), py=m.peeks(H + 0x118))


def show(title, rows, first=1, last=999):
    print('== ' + title)
    print('tick  pad  | TA  CA  TD  CD | set@        spd acc | Speed Acc | TgtFX    StepX    ForceX   AdjX     FinalX   | PosX')
    for r in rows:
        if not (first <= r['tick'] <= last):
            continue
        w = r['w1']
        print('t%02d %04X | %3d %3d %3d %3d | %08X %4d %3d | %5d %3d | %s %s %s %s %s | %s  (y %s fy %s)' % (
            r['tick'], r['hold'], w['ta'], w['ca'], w['td'], w['cd'], w['setp'], w['setspeed'], w['setacc'], w['speed'], w['acc'],
            f(w['tfx']).rjust(8), f(w['stepx']).rjust(8), f(w['fx']).rjust(8), f(w['adjx']).rjust(8), f(w['finx']).rjust(8), f(w['px']), f(w['py']), f(w['fy'])))


def scenario(name):
    R, U, D, L = gl.RIGHT, gl.UP, gl.DOWN, gl.LEFT
    if name == 'release_open':        # the g1b T0 scenario without a crate
        m = gl.world(hero_px=(100, 100), direction=24)
        seq = [(9, R), (4, 0)]
        return name, m, seq
    if name == 'release_open_left':
        m = gl.world(hero_px=(300, 100), direction=8)
        seq = [(9, L), (4, 0)]
        return name, m, seq
    if name == 'release_open_down':
        m = gl.world(hero_px=(100, 100), direction=0)
        seq = [(9, D), (4, 0)]
        return name, m, seq
    if name == 'release_open_up':
        m = gl.world(hero_px=(100, 300), direction=16)
        seq = [(9, U), (4, 0)]
        return name, m, seq
    if name == 'release_diag':        # down-right then release
        m = gl.world(hero_px=(100, 100), direction=0)
        seq = [(9, D | R), (4, 0)]
        return name, m, seq
    if name == 'tap1':                # a one tick tap
        m = gl.world(hero_px=(100, 100), direction=24)
        seq = [(1, R), (5, 0)]
        return name, m, seq
    if name == 'tap2':
        m = gl.world(hero_px=(100, 100), direction=24)
        seq = [(2, R), (5, 0)]
        return name, m, seq
    if name == 'reversal':            # RIGHT x9 then LEFT x5 (no release)
        m = gl.world(hero_px=(100, 100), direction=24)
        seq = [(9, R), (6, L), (3, 0)]
        return name, m, seq
    if name == 'release_regrab':      # release for 1 tick then RIGHT again
        m = gl.world(hero_px=(100, 100), direction=24)
        seq = [(9, R), (1, 0), (5, R), (4, 0)]
        return name, m, seq
    raise SystemExit('unknown scenario ' + name)


def main():
    names = sys.argv[1:] or ['release_open']
    for n in names:
        name, m, seq = scenario(n)
        pads, total = gl.pad_script(seq)
        rows = gl.run(m, total, pads, post=snap)
        show(name, rows)


if __name__ == '__main__':
    main()
