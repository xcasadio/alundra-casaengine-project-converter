"""ladprobe.py - scratch probe: at the start of chosen ticks (before the tick runs), read the hero's box (+0x1E4 offX, +0x1E8 offY, +0x1F0 width, +0x1F4 depth),
PosX/PosY/PosZ, and call the real 0x8003A9E0 (GetTileHeightAtOffset) with (0, -0x10000) and (0, 0), plus the hero floor function inputs.
The function only writes its own stack frame (read in exe.py fn 8003a9e0), so calling it does not change the run (checked: the trace is compared with a run without probes).
usage: python -B ladprobe.py
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import rworld as R
import ladtop as L
from machine import M32

HERO = R.HERO
PX = 65536
LOG = []


def s32(v):
    v &= M32
    return v - (1 << 32) if v & 0x80000000 else v


def probe(tag):
    def f(m, slots):
        offx, offy, w, d = (m.peeks(HERO + o) for o in (0x1E4, 0x1E8, 0x1F0, 0x1F4))
        x, y, z = (m.peeks(HERO + o) for o in (0x114, 0x118, 0x11C))
        h_up = s32(m.call(0x8003A9E0, HERO, 0, (-0x10000) & M32))
        h_0 = s32(m.call(0x8003A9E0, HERO, 0, 0))
        LOG.append('%s: X %.4f Y %.4f Z %.4f | box offX %.4f offY %.4f w %.4f d %.4f -> Y0 %.4f Y1 %.4f | H(0,-1px) = %.4f (raw %08X) H(0,0) = %.4f | MONT guard PosZ <= H: %s' % (
            tag, x / PX, y / PX, z / PX, offx / PX, offy / PX, w / PX, d / PX, (y + offy) / PX, (y + offy + d) / PX, h_up / PX, h_up & M32, h_0 / PX, z <= h_up))
    return f


def main():
    lay = L.ladder_layout(top_hb=4)
    ev = {t: probe('before tick %d' % t) for t in (42, 43, 44, 72, 73, 74, 75, 76, 77)}
    spec = dict(name='probe', desc='', n=80, pad=[(1, R.UP)], layout=lay, hero_px=(100, 392), direction=16, events=ev)
    res = R.run_r(spec)
    spec2 = dict(spec, events={})
    res2 = R.run_r(spec2)
    same = all(a['hero'] == b['hero'] for a, b in zip(res['rows'], res2['rows']))
    LOG.append('trace with probes == trace without probes: %s' % same)
    print('\n'.join(LOG))
    os.makedirs(L.OUT, exist_ok=True)
    with open(os.path.join(L.OUT, 'probe-C.txt'), 'w', encoding='utf-8') as f:
        f.write('\n'.join(LOG) + '\n')


if __name__ == '__main__':
    main()
