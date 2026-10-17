"""g8_events.py - S8: what happens to the carried crate when the hero is hurt (hazard cell), enters deep water (slope kind 4), is locked (dialogue flag [0x800DC4B8] & 0x34), dies (HP 0), stands on a warp tile (+0x188 & 0x80).
Each trigger is a harness write or a cell, documented per scenario; the crate is lifted for real at t11 and carried until the trigger at t=TRIG.
Run: python -B -I g8_events.py"""
import sys, os
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import gl
import ana
from machine import M32

TRIG = 60


def base(cells=None, n=110, walk=None, hp=None, trig_fn=None, hero_px=(100, 100), crate_px=(130, 100)):
    m = gl.world(cells=cells, hero_px=hero_px, direction=24)
    c = gl.add_crate(m, *crate_px)
    seq = [(10, gl.RIGHT), (1, gl.RIGHT | gl.SQUARE)]
    seq += walk if walk else [(n - 11, 0)]
    pads, nn = gl.pad_script(seq)

    def pre(m_, t):
        if trig_fn:
            trig_fn(m_, t)
    rows = gl.run(m, nn, pads, crates=[c], pre=pre)
    return m, c, rows


def report(tag, rows, c, first=TRIG - 2):
    ch_h = ana.changes(rows, 'hero', ['anim_t', 'carried2C', 'slope', 'p188'], first=first)[:10]
    ch_c = ana.changes(rows, c, ['status', 'anim_t', 't24', 'carrier28', 'flags6C'], first=first)[:10]
    last = rows[-1]
    return '%s: hero changes %s ; crate changes %s ; end: hero.+0x2C=%X crate status %d pos (%.2f,%.2f,%.2f)' % (
        tag, ch_h, ch_c, last['hero']['carried2C'], last['c'][c]['status'], last['c'][c]['px'] / 65536, last['c'][c]['py'] / 65536, last['c'][c]['pz'] / 65536)


if __name__ == '__main__':
    out = []
    # H1 hazard: cells wk=0x80 for col >= 6 (x >= 144); hero walks east carrying
    for nm, cell in (('hazard wk=0x80', dict(wk=0x80)), ('hazard gp=0x01', dict(gp=0x01)), ('hazard wk=0x80|gp=0x01', dict(wk=0x80, gp=0x01))):
        cells = lambda col, row, cell=cell: dict(cell) if col >= 6 else None
        m, c, rows = base(cells=cells, walk=[(48, 0), (60, gl.RIGHT)], n=119)
        out.append(report('H1 ' + nm, rows, c, first=48))
    # H2 deep water: gp=0x08 (slope kind 4) for col >= 6
    cells = lambda col, row: dict(gp=0x08) if col >= 6 else None
    m, c, rows = base(cells=cells, walk=[(48, 0), (60, gl.RIGHT)], n=119)
    out.append(report('H2 deep water gp=0x08', rows, c, first=48))
    # H3 locked: [0x800DC4B8] = 4 from t=TRIG to t=TRIG+40 then 0
    def lock(m_, t):
        if t == TRIG:
            m_.poke(0x800DC4B8, 4)
        if t == TRIG + 40:
            m_.poke(0x800DC4B8, 0)
    m, c, rows = base(n=130, trig_fn=lock)
    out.append(report('H3 locked [0x800DC4B8]=4 for 40 ticks', rows, c))
    txt = gl.table(rows, [c], first=TRIG - 1, last=TRIG + 14, hcols=['anim_t', 'carried2C', 'off30', 'off34', 'off38', 'px'], ccols=['px', 'py', 'pz', 'status', 'carrier28'], title='H3 locked')
    open(os.path.join(gl.OUT, 'g8-locked.txt'), 'w').write(txt + '\n')
    # H4 death: hero HP (+0x14) = 0 at t=TRIG
    def die(m_, t):
        if t == TRIG:
            m_.poke(gl.HERO + 0x14, 0)
    m, c, rows = base(n=110, trig_fn=die)
    out.append(report('H4 hero HP=0 at t60', rows, c))
    # H5 warp tile: slope byte 0x88|... : cell (wk 0, gp 0, slope 0x88, h 0) under the hero's feet at col 4..5 (hero at x=109 => col 4)
    cells = lambda col, row: dict(slope=0xC8) if col in (3, 4, 5) else None
    m, c, rows = base(cells=cells, n=110)
    out.append(report('H5 warp tile (slope byte 0xC8)', rows, c, first=1))
    print('\n'.join(out))
    open(os.path.join(gl.OUT, 'g8-events-summary.txt'), 'w').write('\n'.join(out) + '\n')
