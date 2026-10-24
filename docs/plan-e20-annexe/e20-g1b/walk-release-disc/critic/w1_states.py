"""w1_states.py - walk-release-disc W1: the release of the direction in the other states of the hero, on the REAL code of ALUN_CD.EXE (France).
Scenarios: carry (object in the hands), run (Triangle), wall (pushing against a terrain wall), jump (release in the air), swim (deep water cells), slope (a slope cell).
Run from the copy of critic-scenarios:  python -B -I w1_states.py [scenario ...]
Per tick: pad, TA/CA, TargetDirection, Speed/Acc of the AnimationSet in use, ForceX/ForceY (+0xC4/+0xC8), FinalX/FinalY, PosX/PosY/PosZ, +0x13C, ground."""
import sys, os
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.join(HERE, 'oracle'))
import gl
import k0lib as K

H = gl.HERO


def f(v):
    return '%.4f' % (v / 65536)


def snap(m, t, r):
    p = m.peek(H + 0x9C)
    r['w1'] = dict(ta=m.peek(H + 0x88), ca=m.peek(H + 0x90), td=m.peek(H + 0x8C), setspeed=(m.peek(p + 0x8) & 0xFFFF) if p else -1, setacc=((m.peek(p + 0xC) >> 8) & 0xFF) if p else -1,
                   step_x=m.peeks(H + 0xD4), step_y=m.peeks(H + 0xD8),
                   fx=m.peeks(H + 0xC4), fy=m.peeks(H + 0xC8), finx=m.peeks(H + 0xE4), finy=m.peeks(H + 0xE8), px=m.peeks(H + 0x114), py=m.peeks(H + 0x118), pz=m.peeks(H + 0x11C),
                   adj=m.peeks(H + 0x13C), ground=m.peeks(H + 0x144), slope=m.peeks(H + 0x18C), xcoll=m.peek(H + 0x130), carried=m.peek(H + 0x2C))


def show(title, rows, first=1, last=999):
    print('== ' + title)
    print('tick pad  | TA  CA  TD | Spd Acc | StepX    StepY    | ForceX   ForceY   | FinalX   FinalY   | PosX       PosY       PosZ   | adj gr slope xcoll    carried')
    for r in rows:
        if not (first <= r['tick'] <= last):
            continue
        w = r['w1']
        print('t%02d %04X | %3d %3d %3d | %3d %3d | %8s %8s | %8s %8s | %8s %8s | %10s %10s %6.1f | %3d %2d %5d %-8X %-8X' % (
            r['tick'], r['hold'], w['ta'], w['ca'], w['td'], w['setspeed'], w['setacc'], f(w['step_x']).rjust(8), f(w['step_y']).rjust(8), f(w['fx']).rjust(8), f(w['fy']).rjust(8),
            f(w['finx']).rjust(8), f(w['finy']).rjust(8), f(w['px']), f(w['py']), w['pz'] / 65536, w['adj'], w['ground'], w['slope'], w['xcoll'], w['carried']))


def scenario(name):
    R, U, D, L, TRI, CROSS, SQ = gl.RIGHT, gl.UP, gl.DOWN, gl.LEFT, gl.TRI, gl.CROSS, gl.SQUARE
    if name == 'carry':      # lift a crate (Square while blocked by it), wait for the gesture to end, walk east 9 ticks with it, release
        m = gl.world(hero_px=(200, 200), direction=24)
        c = gl.add_crate(m, 230, 200)
        seq = [(10, R), (1, R | SQ), (49, 0), (9, R), (6, 0)]
        return name, m, seq, [c], 55
    if name == 'carry_open':  # same with the crate far (no contact) -> not a lift; kept as a control of 'carry' (hero walks normally)
        m = gl.world(hero_px=(200, 200), direction=24)
        seq = [(9, R), (6, 0)]
        return name, m, seq, [], 1
    if name == 'run':        # Triangle + RIGHT held: preparation (0x29, 25 ticks) then sprint (0x03); then release everything
        m = gl.world(hero_px=(100, 100), direction=24)
        seq = [(40, R | TRI), (22, 0)]
        return name, m, seq, [], 38
    if name == 'wall':       # terrain wall to the east: cells of height 4 (64 px) from column 8 (x >= 192): the hero walks into it, then releases
        def cells(col, row):
            return dict(h=4) if col >= 8 else None
        m = gl.world(cells=cells, hero_px=(150, 100), direction=24)
        seq = [(14, R), (6, 0)]
        return name, m, seq, [], 1
    if name == 'jump':       # RIGHT held 5 ticks, Cross edge at tick 6 (jump with a direction), the direction released at tick 9 in the air
        m = gl.world(hero_px=(100, 100), direction=24)
        seq = [(5, R), (1, R | CROSS), (2, R), (14, 0)]
        return name, m, seq, [], 1
    if name == 'slope':      # a strip of cells with the slope byte 100 (TileAttributes 4: the slide table pushes 61440 units west, ApplyEntityForces 0x800366FC); east walk then release
        def cells(col, row):
            return dict(slope=100) if 4 <= col <= 20 else None
        m = gl.world(cells=cells, hero_px=(150, 100), direction=24)
        seq = [(9, R), (6, 0)]
        return name, m, seq, [], 1
    if name == 'swim':       # deep water (slope byte 4) field east of x = 190
        def cells(col, row):
            return dict(slope=4, gp=0x08) if col >= 8 else None
        m = gl.world(cells=cells, hero_px=(150, 100), direction=24)
        seq = [(24, R), (10, 0)]
        return name, m, seq, [], 1
    raise SystemExit('unknown scenario ' + name)


def main():
    names = sys.argv[1:] or ['carry']
    for n in names:
        name, m, seq, crates, first = scenario(n)
        pads, total = gl.pad_script(seq)
        rows = gl.run(m, total, pads, crates=crates, post=snap)
        show(name, rows, first=first)


if __name__ == '__main__':
    main()
