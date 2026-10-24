"""c5_status4.py - E20.g T0 (added 2026-10-24, to kill the mutant M06): the loop of PlayerTryAction (0x8002EF64-0x8002EFC0) accepts an entity when (status - 2) < 2, i.e. status 2 or 3;
the mutant sltiu 3 also takes status 4.  In a whole tick the status-4 entity never has +0x12C == hero when the loop runs (the tick clears it: see the first rows), so this script CALLS the real
PlayerTryAction 0x8002EEAC directly (as c1 does with 0x8002F884), with hero.+0x2C = 0, hero.+0x130 = 0 and the crate (REAL map-record spawner) standing on the hero: crate.+0x12C = hero, crate.+0x20 = 0,
crate status S in 2, 3, 4, 5.  Prediction written before the run: the call returns 1 and sets hero.+0x2C = crate for S = 2 and 3, and returns 0 for S = 4 and 5.
Rows 'tick': the same crate in a whole tick (the g9 G set-up), status poked each tick.  Run: python -B -I c5_status4.py"""
import sys, os
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.join(HERE, 'oracle'))
import gl
import ana
import k0lib as K
from machine import M32, s32

if __name__ == '__main__':
    out = []
    for S in (2, 3, 4, 5):
        m = gl.world(); c = gl.add_crate(m, 100, 100)
        e = K.ent(c)
        m.poke(gl.HERO + 0x2C, 0); m.poke(gl.HERO + 0x130, 0)
        m.poke(e + 0x10, S); m.poke(e + 0x12C, gl.HERO); m.poke(e + 0x20, 0)
        r = m.call(0x8002EEAC)
        out.append('direct call, status %d: returns %d ; hero.+0x2C %X (crate entity %X)' % (S, r, m.peek(gl.HERO + 0x2C), e))
    for S in (2, 3, 4, 5):
        m = gl.world(); c = gl.add_crate(m, 100, 100)
        e = K.ent(c)
        hero_top = m.peeks(gl.HERO + 0x11C) + m.peeks(gl.HERO + 0x1F8)
        m.poke(e + 0x11C, (hero_top + 1) & M32); m.poke(e + 0x1E0, (hero_top + 1 + m.peeks(e + 0x1EC)) & M32)
        pads, n = gl.pad_script([(10, 0)])
        rows = gl.run(m, n, pads, crates=[c], pre=lambda mm, t, e=e, S=S: mm.poke(e + 0x10, S))
        out.append('whole tick, status %d: support per tick %s ; lifted at %s' % (S, [hex(r['c'][c]['support']) for r in rows[:3]], ana.first_tick(rows, lambda r: r['hero']['carried2C'] != 0)))
    print('\n'.join(out))
    open(os.path.join(gl.OUT, 'c5-status4.txt'), 'w').write('\n'.join(out) + '\n')
