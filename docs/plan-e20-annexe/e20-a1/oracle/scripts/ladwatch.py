"""ladwatch.py - scratch probe: logs, for chosen ticks of the C-short-up-held run (cliff hb 4, ladder strip hb 2), every store of the real code into chosen hero fields,
with the PC of the store and the call stack (function entries pushed by jal/jalr, popped by jr ra).  Read-only observation: the wrapped ww only logs then calls the original.
usage: python -B ladwatch.py [ticks...]   (default 74 75 76 77)
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
FIELDS = {0x6C: 'Flags', 0x88: 'TargetAnim', 0x90: 'CurAnim', 0xB8: 'ForceZ', 0xC4: 'ForceX', 0xC8: 'ForceY', 0xBC: 'TargetForceX', 0xC0: 'TargetForceY',
          0xE4: 'FinalForceX', 0xE8: 'FinalForceY', 0xEC: 'FinalForceZ', 0xF8: 'IZF', 0x114: 'PosX', 0x118: 'PosY', 0x11C: 'PosZ',
          0x134: 'Floor', 0x138: 'Terrain', 0x13C: 'ForceAdj', 0x140: 'CollZ', 0x144: 'IsOnGround', 0x18C: 'Slope'}
LOG = []


def install(m, ticks):
    stack = []
    orig_exec = m.exec1
    orig_ww = m.ww

    def ex(pc, w):
        br = orig_exec(pc, w)
        op = w >> 26
        if br is not None and br[0] == 'j':
            if op == 3 or (op == 0 and (w & 63) == 9):
                stack.append(br[1])
            elif op == 0 and (w & 63) == 8 and ((w >> 21) & 31) == 31:
                if stack:
                    stack.pop()
        return br

    def ww(a, v):
        if m.tick_no in ticks and HERO <= a < HERO + 0x294 and (a - HERO) in FIELDS:
            old = m.peek(a)
            if (old & M32) != (v & M32):
                LOG.append('tick %d pc %08X  %-12s %08X -> %08X   stack %s' % (m.tick_no, m.cur_pc, FIELDS[a - HERO], old & M32, v & M32,
                                                                        '>'.join('%08X' % s for s in stack[-6:])))
        return orig_ww(a, v)
    m.exec1 = ex
    m.ww = ww


def main(argv):
    ticks = set(int(a) for a in argv) or {74, 75, 76, 77}
    first = min(ticks)
    lay = L.ladder_layout(top_hb=4)

    def ev(m, slots):
        install(m, ticks)
        LOG.append('# installed before tick %d' % first)
    spec = dict(name='watch', desc='', n=max(ticks), pad=[(1, R.UP)], layout=lay, hero_px=(100, 392), direction=16, events={first: ev})
    res = R.run_r(spec)
    spec2 = dict(spec, events={})
    res2 = R.run_r(spec2)
    same = all(a['hero'] == b['hero'] for a, b in zip(res['rows'], res2['rows']))
    LOG.append('trace with the watcher == trace without: %s' % same)
    print('\n'.join(LOG))
    with open(os.path.join(L.OUT, 'watch-C-%s.txt' % '-'.join(str(t) for t in sorted(ticks))), 'w', encoding='utf-8') as f:
        f.write('\n'.join(LOG) + '\n')


if __name__ == '__main__':
    main(sys.argv[1:])
