"""thrown_damage.py - O-E20-19: does a thrown crate hurt what it touches? REAL hit pass 0x80039300 sets target.+0x224 = crate and crate.+0x220++; the damage is the target's own call of ComputeNewHp 0x8003A374,
which the harness performs for the target at the tick of the touch (the Beannoid's real handler is an inert stub in this oracle). Reads HP before/after. Run: python -B -I thrown_damage.py"""
import sys, os
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import gl
import k0lib as K
from machine import M32

def run(tx, sprite_map='map_14', sprite=79, hp_override=None):
    m = gl.world(hero_px=(100, 100), direction=24)
    c = gl.add_crate(m, 130, 100, rec_addr=0x801F0000, ent_addr=0x801F8000, ptr_addr=0x801F9000)
    pl, _ = K.spawn_record_entities(m, sprite_map, sprite, [(10, 11, 0)])
    p = pl[0]; pe = K.ent(p)
    K.poke_entity(m, p, px=tx * 65536, py=100 * 65536)
    m.poke(pe + 0x1D8, (tx * 65536 + m.peeks(pe + 0x1E4)) & M32); m.poke(pe + 0x1DC, (100 * 65536 + m.peeks(pe + 0x1E8)) & M32)
    T = m.call(0x800370C4, pe)
    m.poke(pe + 0x138, T); m.poke(pe + 0x11C, T + 1); m.poke(pe + 0x1E0, T + 1)
    if hp_override is not None:
        m.poke(pe + 0x14, hp_override)
    seq = [(10, gl.RIGHT), (1, gl.RIGHT | gl.SQUARE), (49, 0), (1, gl.RIGHT | gl.SQUARE), (40, 0)]
    pads, nn = gl.pad_script(seq)
    res = {}
    def post(m_, t, r):
        if m_.peek(pe + 0x224) != 0 and 'hp0' not in res:
            res['tick'] = t
            res['attacker'] = hex(m_.peek(pe + 0x224))
            res['hp0'] = m_.peeks(pe + 0x14)
            res['atk_rec_ptr'] = hex(m_.peek(m_.peek(pe + 0x224) + 0x1C8))
            res['atk_rec_bytes'] = [m_.peek(m_.peek(m_.peek(pe + 0x224) + 0x1C8) + 4 * k) for k in range(2)]
            dead = m_.call(0x8003A374, pe)
            res['hp1'] = m_.peeks(pe + 0x14)
            res['dead'] = dead
            res['sheet_responses'] = [m_.rb(m_.peek(pe + 0x1C4) + 0x12 + k) for k in range(12)]
    gl.run(m, nn, pads, crates=[c], extra=[p], post=post)
    return res

if __name__ == '__main__':
    out = []
    for hp in (None,):
        for tx in (165,):
            r = run(tx, hp_override=hp)
            out.append('Beannoid(map_14 sprite 79) at x=%d: %s' % (tx, r))
    print('\n'.join(out))
    open(os.path.join(HERE, 'out', 'thrown-damage.txt'), 'w').write('\n'.join(out) + '\n')
