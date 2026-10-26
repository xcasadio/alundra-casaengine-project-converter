"""s2_drop_on_torch.py - read-only replay (copy of the g2c oracle machine): a water drop (map 92 sprite 135 record 15, native; REAL A19 + C71) falls on (a) the hero CARRYING a torch (map 86 sprite 137,
REAL C70 + E20 + E0, REAL MovePlayer lifts it), (b) the hero WITHOUT a torch, (c) a torch lying on the ground, (d) a torch lying on the ground with the hero standing next to it.
Harness writes (documented): the pad words; the drop's X/Y/Z (Z = +160 px above the ground, as the cycle restores it) ; for (c)/(d) the torch's position; nothing else.
Run: python -B -I -X utf8 s2_drop_on_torch.py carry|nocarry|posed [n]
Columns: H = hero (status, anim_t = state/animation id, carried +0x2C, hp +0x14, px, pz, ground, support), T = torch (status, +0x274, anim_t, AC, flags, carrier +0x28, pz, ground, support, hitc),
D = drop (b274 = its state, anim, flags, pz, ground, collz, support)."""
import os, sys
HERE = os.path.dirname(os.path.abspath(__file__))
ORC = os.path.join(HERE, 'replay', 'work', 'orc')
sys.path.insert(0, ORC)
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.join(HERE, 'replay'))
import gl
import k0lib as K
import c4_scen as S
from machine import M32
from s1_drop_cycle import add_obj, rd8

A19, C71 = 0x800619D0, 0x8007B834
C70, E20, E0 = 0x8007B7B0, 0x8007F6C8, 0x8007ED10
HERO = K.ent(0)


def run(mode, n=170, dropx=None, dropy=100, tx=130, ty=100, nodrop=False, trace_hero=True, press=None):
    S.widen('map_86', 137)
    S.widen('map_92', 135)
    m = gl.world(keep={A19, C71, C70, E20, E0})
    t = gl.add_crate(m, tx, ty, mapname='map_86', sprite=137)
    te = K.ent(t)
    if mode == 'carry' and press:
        # the player presses the Square (throw) at tick `press` while the torch is being extinguished
        seq = [(10, gl.RIGHT), (1, gl.RIGHT | gl.SQUARE), (press - 12, 0), (1, gl.SQUARE), (n - press, 0)]
    elif mode == 'carry':
        seq = [(10, gl.RIGHT), (1, gl.RIGHT | gl.SQUARE), (n - 11, 0)]
    elif mode == 'nocarry':
        seq = [(10, gl.RIGHT), (n - 10, 0)]
    else:   # posed: the hero stays away
        seq = [(n, 0)]
    pads, nn = gl.pad_script(seq)
    # preliminary run to know where the hero stands (deterministic): the drop is placed above the hero (carry / nocarry) or above the torch (posed)
    if dropx is None:
        if mode == 'posed':
            dropx, dropy = tx, ty
        else:
            m0 = gl.world(keep={C70, E20, E0})
            t0 = gl.add_crate(m0, tx, ty, mapname='map_86', sprite=137)
            rows0 = gl.run(m0, 30, pads, crates=[t0])
            dropx = round(rows0[-1]['hero']['px'] / 65536)
            dropy = round(rows0[-1]['hero']['py'] / 65536)
    d = None
    if not nodrop:
        d = add_obj(m, dropx, dropy, 'map_92', 135, 1, lambda T: T + 1 + 160 * 65536, rec_addr=0x801F0000, ent_addr=0x801F8000, ptr_addr=0x801F9000)
    de = K.ent(d) if d is not None else None
    log = []

    def post(mm, tk, r):
        h = r['hero']
        tt = r['c'][t]
        row = dict(tick=tk,
                   H=(mm.peek(HERO + 0x10), h['anim_t'], hex(h['carried2C']), mm.peeks(HERO + 0x14), round(h['px'] / 65536, 1), round(h['pz'] / 65536, 1), h['ground'], hex(h['support'])),
                   T=(tt['status'], rd8(mm, te + 0x274), tt['anim_t'], tt['reset_AC'], hex(tt['flags6C']), hex(tt['carrier28']), round(tt['pz'] / 65536, 1), tt['ground'], hex(tt['support']), tt['hitc']),
                   D=None,
                   J=[x for x in r['journal'] if not x.startswith('stub')])
        if de is not None:
            row['D'] = (mm.peek(de + 0x10), rd8(mm, de + 0x274), mm.peek(de + 0x88), hex(mm.peek(de + 0x6C)), round(mm.peeks(de + 0x11C) / 65536, 1), mm.peek(de + 0x144), mm.peek(de + 0x140), hex(mm.peek(de + 0x12C)))
        log.append(row)

    rows = gl.run(m, nn, pads, crates=[t], post=post)
    return m, t, d, rows, log, (dropx, dropy)


def show(log, lo=1, hi=10 ** 9, tag=''):
    prev = None
    print('# H=(status, state/anim, carried2C, hp, x, z, ground, support)  T=(status, b274, anim, AC, flags, carrier28, z, ground, support, hitc)  D=(status, b274, anim, flags, z, ground, collz, support)')
    for r in log:
        if not (lo <= r['tick'] <= hi):
            continue
        key = (r['H'][:4] + r['H'][6:7], r['T'][:6] + r['T'][7:], (r['D'][:3] + (r['D'][3],) + r['D'][5:]) if r['D'] else None)
        if key != prev or r['J']:
            print('t%03d H=%s T=%s D=%s %s' % (r['tick'], r['H'], r['T'], r['D'], r['J'] or ''))
            prev = key


if __name__ == '__main__':
    mode = sys.argv[1] if len(sys.argv) > 1 else 'carry'
    n = int(sys.argv[2]) if len(sys.argv) > 2 else 170
    pr = int(sys.argv[3]) if len(sys.argv) > 3 else None
    m, t, d, rows, log, pos = run(mode, n, press=pr)
    print('mode %s, drop at %s' % (mode, pos))
    show(log)
    act = [(sl, hex(m.peek(K.ent(sl) + 0x68)), m.peek(K.ent(sl) + 0x10)) for sl in K.active(m)]
    print('active slots at the end (slot, type, status): %s  -> a flame would be a slot of type 0xc' % act)
