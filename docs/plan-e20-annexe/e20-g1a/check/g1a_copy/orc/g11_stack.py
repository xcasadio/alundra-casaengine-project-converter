"""g11_stack.py - S11: a stack of two crates (392 barricade rows 19/20 are 2 high): the hero walks into the stack and presses Square.  Which crate is lifted?  What does the other do (falls? rides?)
Lower crate L: slot order variants (L first / U first in the entity list).  U stands on L (Z = L top + 1 raw unit), both spawned by the REAL map-record spawner, placed by the harness.
Run: python -B -I g11_stack.py"""
import sys, os
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import gl
import ana
import k0lib as K
from machine import M32


def stack(order='LU', n=120, hero_px=(100, 100), sq_tick=None, extra_after=None):
    m = gl.world(hero_px=hero_px)
    first = gl.add_crate(m, 130, 100)
    second = gl.add_crate(m, 130, 100, rec_addr=0x801F0000, ent_addr=0x801F8000, ptr_addr=0x801F9000)
    # the first spawned entity is the lower one if order == 'LU'
    L, U = (first, second) if order == 'LU' else (second, first)
    le, ue = K.ent(L), K.ent(U)
    ltop = m.peeks(le + 0x11C) + m.peeks(le + 0x1F8)                 # PosZ + Depth
    m.poke(ue + 0x11C, (ltop + 1) & M32); m.poke(ue + 0x1E0, (ltop + 1 + m.peeks(ue + 0x1EC)) & M32)
    seq = [(10, gl.RIGHT), (1, gl.RIGHT | gl.SQUARE), (n - 11, 0)]
    pads, nn = gl.pad_script(seq)
    rows = gl.run(m, nn, pads, crates=[L, U])
    return m, L, U, rows


if __name__ == '__main__':
    out = []
    for order in ('LU', 'UL'):
        m, L, U, rows = stack(order)
        lifted = [(nm, s) for nm, s in (('L', L), ('U', U)) if rows[20]['c'][s]['carrier28'] != 0]
        out.append('order %s (slot of lower=%d upper=%d): hero xcoll at t10 %X ; lifted at t11: %s ; support of U per tick %s' % (
            order, L, U, rows[9]['hero']['xcoll'], [nm for nm, s in lifted], [hex(r['c'][U]['support']) for r in rows[:3]]))
        for nm, s in (('L', L), ('U', U)):
            out.append('   %s: Z per 5 ticks %s ; status changes %s ; carrier %X at t30' % (nm, ana.series(rows, s, 'pz', first=1, last=70, scale=65536.0)[::5], ana.changes(rows, s, ['status', 'anim_t'], first=2)[:4], rows[29]['c'][s]['carrier28']))
        txt = gl.table(rows, [L, U], first=9, last=22, hcols=['anim_t', 'carried2C', 'px', 'xcoll'], ccols=['carrier28', 'px', 'pz', 'support', 'ground', 'status'], title='STACK order ' + order)
        open(os.path.join(gl.OUT, 'g11-stack-%s.txt' % order), 'w').write(txt + '\n')
    print('\n'.join(out))
    open(os.path.join(gl.OUT, 'g11-stack-summary.txt'), 'w').write('\n'.join(out) + '\n')
