"""c1_oblique.py - critic-recon: does the REAL XY move (0x80037730) let the hero slide along a crate face when he pushes OBLIQUELY into it?
The DLL's controller resolves X then Y separately (CharacterControllerComponent.ResolveHorizontalDisplacementAgainstField), so the free axis keeps moving.
Real map 392 geometry, real crates (records 4..11 of the barricade), hero inside the doorway east of the column. Output out/c1_oblique.txt"""
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import lib392 as L

OUT = []


def P(s=''):
    OUT.append(s)
    print(s, flush=True)


def name_of(S, addr):
    for rid, slot in S.items():
        if L.K.ent(slot) == addr:
            return 'rec%d' % rid
    return '-' if addr == 0 else hex(addr)


def scen(title, start_xy, seq, direction=8, ticks_shown=None):
    m, S = L.world392(hero_px=(int(start_xy[0]), int(start_xy[1])), direction=direction)
    h = L.K.ent(0)
    m.poke(h + 0x114, int(round(start_xy[0] * 65536)) & 0xFFFFFFFF)
    m.poke(h + 0x118, int(round(start_xy[1] * 65536)) & 0xFFFFFFFF)
    pads, n = L.pad_seq(seq)
    rows = L.gl.run(m, n, pads, crates=list(S.values()))
    P('== ' + title)
    for r in rows:
        if ticks_shown is None or r['tick'] in ticks_shown:
            H = r['hero']
            P('t%02d pad=%04X x=%.4f y=%.4f z=%.3f adj=%d xcoll=%s' % (r['tick'], r['hold'], H['px'] / 65536, H['py'] / 65536, H['pz'] / 65536, H['adj'], name_of(S, H['xcoll'])))
    return rows


shown = set(range(1, 41))
scen('A: start (664, 314), LEFT+UP 40 ticks (rec6 east face at x=647, door north wall stops y at 295)', (664.0, 314.0), [(40, L.LEFT | L.UP)], ticks_shown=shown)
scen('B: start (664, 300), LEFT+DOWN 40 ticks (rec5/rec6/rec7 east faces, door south wall)', (664.0, 300.0), [(40, L.LEFT | L.DOWN)], ticks_shown=shown)
scen('C: start (657, 312) flush on rec6, UP 20 ticks (cardinal along the crate face)', (657.0, 312.0), [(20, L.UP)], direction=16, ticks_shown=set(range(1, 21)))
open(os.path.join(L.OUT, 'c1_oblique.txt'), 'w', encoding='utf-8').write('\n'.join(OUT) + '\n')
