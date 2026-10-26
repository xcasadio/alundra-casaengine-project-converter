"""f1_flame.py - read-only replay (copy of the g2c oracle machine): the torch thrown flat (REAL C70 0x8007B7B0, REAL E20 0x8007F6C8), with the handlers of the flame ALSO real:
E0 (0x8007ED10, the deactivation that destroys), C60 (0x8007A8A0) and E9 (0x8007EF50), the two handlers of f6b for the type 12.
Question: does the flame E20 spawns run C60 / E9 (it would emit effect 17 / explode), and when does it die?
Variant 'nozero' neutralises the six stores of E20 (0x8007F744, the sw $zero,0x70($v0) of the loop) in the machine's memory: what would happen if the flame kept its codes.
Run: python -B -I -X utf8 f1_flame.py [zero|nozero]"""
import os, sys
HERE = os.path.dirname(os.path.abspath(__file__))
ORC = os.path.join(HERE, 'work', 'orc')
sys.path.insert(0, ORC)
sys.path.insert(0, HERE)
import gl
import k0lib as K
import c4_scen as S

C70, E20, E0, C60, E9 = 0x8007B7B0, 0x8007F6C8, 0x8007ED10, 0x8007A8A0, 0x8007EF50


def flame_slot(m):
    for s_ in K.active(m):
        if m.peek(K.ent(s_) + 0x68) == 0xC:
            return s_
    return None


def run(variant, n=260):
    S.widen('map_86', 137)
    m = gl.world(keep={C70, E20, E0, C60, E9})
    if variant == 'nozero':
        m.poke(0x8007F744, 0)       # the store of the loop becomes a nop
    c = gl.add_crate(m, 130, 100, mapname='map_86', sprite=137)
    seq = [(10, gl.RIGHT), (1, gl.RIGHT | gl.SQUARE), (49, 0), (1, 0 | gl.SQUARE), (n - 61, 0)]
    pads, nn = gl.pad_script(seq)
    track = {}

    def post(mm, t, r):
        fs = flame_slot(mm)
        if fs is not None:
            e = K.ent(fs)
            track[t] = (fs, mm.peek(e + 0x10), mm.peek(e + 0x88), hex(mm.peek(e + 0x6C)), [mm.peek(e + 0x70 + 4 * i) for i in range(6)], mm.peek(e + 0xAC))
        else:
            track[t] = None

    rows = gl.run(m, nn, pads, crates=[c], post=post)
    life = [t for t in sorted(track) if track[t] is not None]
    out = ['variant %s' % variant]
    if life:
        out.append('flame present at ticks %d..%d (%d ticks)' % (life[0], life[-1], len(life)))
        out.append('first tick  %d: slot,status,anim,flags,codes[A,B,C,D,E,F],+0xAC = %s' % (life[0], track[life[0]]))
        out.append('tick +10    : %s' % (track.get(life[0] + 10),))
        out.append('last tick   %d: %s' % (life[-1], track[life[-1]]))
        out.append('tick after  %d: %s' % (life[-1] + 1, track.get(life[-1] + 1)))
    else:
        out.append('no flame')
    fx = [(r['tick'], [x for x in r['journal'] if 'fx' in x or 'eff' in x or 'snd' in x]) for r in rows if r['tick'] >= 80 and [x for x in r['journal'] if ('fx' in x or 'eff' in x or 'snd' in x)]]
    out.append('journal of effects/sounds after t80 (first 14): %s' % (fx[:14],))
    return out


if __name__ == '__main__':
    variants = sys.argv[1:] or ['zero', 'nozero']
    for v in variants:
        print('\n'.join(run(v)))
        print()
