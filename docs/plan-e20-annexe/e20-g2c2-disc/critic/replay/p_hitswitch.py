"""p_hitswitch.py (critic) - read-only: the torch (REAL C70, REAL E20, REAL E0) thrown moving east at a CLASS-A target placed in its path, by the REAL hit pass 0x80039300.
Target = the 'Interrupteur (allumage)' of map 92 (sprite 51, ClassA, response 16 to the nibble 6) or a bramble-like record is not available (global page), so the switch is the case.
Question: when does the torch's own attack register (target +0x224 = torch, torch +0x220), when does the torch deactivate (DeactivateOnHit), and where/when is the flame born?
Run: python -B -I -X utf8 p_hitswitch.py [tx ...]"""
import os, sys
HERE = os.path.dirname(os.path.abspath(__file__))
ORC = os.path.join(HERE, 'work', 'orc')
sys.path.insert(0, ORC)
sys.path.insert(0, HERE)
import gl
import k0lib as K
import c4_scen as S
from machine import M32

C70, E20, E0 = 0x8007B7B0, 0x8007F6C8, 0x8007ED10


def flame_slot(m):
    for s_ in K.active(m):
        if m.peek(K.ent(s_) + 0x68) == 0xC:
            return s_
    return None


def run(tx, ty=100, n=140):
    S.widen('map_86', 137)
    m = gl.world(keep={C70, E20, E0})
    c = gl.add_crate(m, 130, 100, mapname='map_86', sprite=137, rec_addr=0x801F0000, ent_addr=0x801F8000, ptr_addr=0x801F9000)
    pl, _ = K.spawn_record_entities(m, 'map_92', 51, [(10, 11, 0)])
    p = pl[0]; pe = K.ent(p)
    K.poke_entity(m, p, px=tx * 65536, py=ty * 65536)
    m.poke(pe + 0x1D8, (tx * 65536 + m.peeks(pe + 0x1E4)) & M32); m.poke(pe + 0x1DC, (ty * 65536 + m.peeks(pe + 0x1E8)) & M32)
    T = m.call(0x800370C4, pe)
    m.poke(pe + 0x138, T); m.poke(pe + 0x11C, T + 1); m.poke(pe + 0x1E0, T + 1)
    seq = [(10, gl.RIGHT), (1, gl.RIGHT | gl.SQUARE), (49, 0), (1, gl.RIGHT | gl.SQUARE), (n - 61, 0)]
    pads, nn = gl.pad_script(seq)
    log = []

    def post(mm, t, r):
        e = K.ent(c)
        fs = flame_slot(mm)
        fl = None
        if fs is not None:
            fe = K.ent(fs)
            fl = (mm.peek(fe + 0x10), mm.peek(fe + 0x88), round(mm.peeks(fe + 0x114) / 65536, 1), round(mm.peeks(fe + 0x118) / 65536, 1), mm.peek(fe + 0x220), hex(mm.peek(fe + 0x1C8)))
        log.append((t, mm.peek(e + 0x10), mm.peek(e + 0x88), hex(mm.peek(e + 0x6C)), mm.peek(e + 0x220), round(mm.peeks(e + 0x114) / 65536, 1), round(mm.peeks(e + 0x11C) / 65536, 1), hex(mm.peek(pe + 0x224)), mm.peek(pe + 0x1D0), fl))
    gl.run(m, nn, pads, crates=[c], extra=[p], post=post)
    out = ['target switch at x=%d: (tick, torch status, anim, flags, HitCounter, x, z, target+0x224, target+0x1D0, flame(status, anim, x, y, hitc, atk))' % tx]
    prev = None
    for x in log:
        key = x[1:5] + x[7:8] + (x[9] is not None,)
        if x[0] >= 58 and key != prev:
            out.append(str(x))
        prev = key
    return out


if __name__ == '__main__':
    for tx in [int(a) for a in sys.argv[1:]] or [165, 150]:
        print('\n'.join(run(tx)))
        print()
