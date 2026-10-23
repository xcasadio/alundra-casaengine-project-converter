"""zedge.py - E20.e rev3 T0 (b): Q-E6, the frame of Z of the band test of CheckAndTriggerTileEffect (0x8003A7B0), on the machine of the E20 oracles.

(1) the REAL function called on the machine's hero entity with a box written by hand: a cuttable cell of height byte H (zc = (H << 20) + 0x80000), and the box lower edge bz
    (+0x204) and upper edge bz + bh at zc - 1, zc, zc + 1.  The binary skips the cell when `zc < bz` or `bz + bh < zc` (signed): the table below is MEASURED, not read from the rule.
(2) the same boundary seen from the DLL: the DLL keeps PosZ = binary - 1 (ADR-0026), so HitBoxZ_DLL = bz - 1.  The measured rule in the DLL's frame is: cut iff
    HitBoxZ_DLL + 1 <= zc <= HitBoxZ_DLL + 1 + bh.  With the box edge exactly on zc in the DLL's frame (HitBoxZ_DLL == zc) the binary SKIPS (bz = zc + 1): the naive rule (HitBoxZ_DLL as is) would cut.
(3) the REAL hero, REAL swing (basic sword, facing east), on flat ground of height byte g with a row of cuttable cells of height byte H beside it: which (g, H) cut.
Writes out/zedge.json and out/zedge.txt.  Nothing here knows an expected value of the DLL.
"""
import json
import os
import sys

import cutlib as L

P, W, C = L.P, L.W, L.C
OUT = L.OUT


def machine_with_cell(height, col=10, row=10):
    m, meta = P.setup(item=None, hero_px=(100, 100), seed=0x12345678, cells=lambda c, r: ((height, 0, 2, 0) if (c, r) == (col, row) else (0, 0, 0, 0)))
    a = W.TILES + 0x604 + row * W.ROW + col * 8
    m.poke16(a + 6, 0x1234)                       # a wall word that the cut must overwrite with 0xFFFF
    m.hooks.pop(L.LOOT, None)                     # the real loot runs (it draws), the objects it cannot create come back null: irrelevant here
    return m, a


def decide(height, bz, top, col=10, row=10):
    """call the real CATE on the hero entity with the box (x, y inside the cell, z = bz, bh = top - bz): -> cut? """
    m, a = machine_with_cell(height, col, row)
    e = L.ENT0
    m.poke(e + 0x1D4, 0x80400000)
    m.poke(e + 0x1FC, (col * 24 + 6) << 16)
    m.poke(e + 0x200, (row * 16 + 4) << 16)
    m.poke(e + 0x204, bz & L.M32)
    m.poke(e + 0x214, (4 << 16) - 1)
    m.poke(e + 0x218, (4 << 16) - 1)
    m.poke(e + 0x21C, (top - bz) & L.M32)
    j0 = len(m.journal)
    m.call(L.CATE, e)
    cut = (m.rh(a) & 2) == 0 and m.rh(a + 6) == 0xFFFF
    fx = [j for j in m.journal[j0:] if j[1] == 'effect_3BDD8']
    return cut, len(fx)


def real_hero(g, H, direction='east'):
    cells = lambda c, r: ((H, 0, 2, 0) if (c in (5, 6, 7) and r in (5, 6, 7)) else (g, 0, 0, 0))
    spec = dict(item=None, n=30, holds={1: L.SQUARE}, after=0, hero_px=(100, 100), direction=L.DIRS[direction], cells=cells, seed=0x12345678, ticker=L.ticker_with_step)
    log = L.Log()
    spec['init'] = [L.install_spies(log, True)]
    res = P.run(spec)
    boxes = [(c['tick'], c['box']['z'], c['box']['h']) for c in log.cate if c['slot'] == 0][:1]
    return sum(c['cuts'] for c in log.cate), boxes


def main():
    out = dict(synthetic_lower=[], synthetic_upper=[], real_hero={})
    lines = []
    for H in (0, 1, 2):
        zc = (H << 20) + 0x80000
        lines.append('# cell height byte %d: zc = 0x%X' % (H, zc))
        for dz in (-1, 0, 1):
            cut, n = decide(H, zc + dz, zc + 0x100000)
            out['synthetic_lower'].append(dict(H=H, zc=zc, bz_minus_zc=dz, hitboxz_dll_minus_zc=dz - 1, cut=cut))
            lines.append('  lower edge  bz = zc%+d   (HitBoxZ_DLL = zc%+d)   top = zc + 0x100000 : %s' % (dz, dz - 1, 'CUT' if cut else 'skip'))
        for dt in (-1, 0, 1):
            cut, n = decide(H, zc - 0x100000, zc + dt)
            out['synthetic_upper'].append(dict(H=H, zc=zc, top_minus_zc=dt, cut=cut))
            lines.append('  upper edge  bz + bh = zc%+d  (bz = zc - 0x100000) : %s' % (dt, 'CUT' if cut else 'skip'))
    lines.append('# the real hero, basic sword east, flat ground g, cuttable cells of height H (cuts in 30 ticks, first box (tick, bz, bh))')
    for g in range(0, 4):
        for H in range(0, 5):
            n, boxes = real_hero(g, H)
            out['real_hero']['g%d_H%d' % (g, H)] = dict(cuts=n, first_box=boxes)
            lines.append('  g=%d H=%d : %d cuts  first call box (tick, bz, bh) = %s' % (g, H, n, boxes))
    json.dump(out, open(os.path.join(OUT, 'zedge.json'), 'w'), indent=1, sort_keys=True)
    open(os.path.join(OUT, 'zedge.txt'), 'w').write('\n'.join(lines) + '\n')
    print('\n'.join(lines))


if __name__ == '__main__':
    main()
