"""g4a_route.py - E20.g4a T0 (b): the route of the story through the barricade by the JUMP, redone with the 12 records of map 392 on the REAL code of ALUN_CD.EXE
(France) in the E20 oracle machine (harness of my392.py, which makes all 12 records spawn in file order, rec9 included).
From the hero's place after B1's walk (775.125, 216, z 64) [the DLL arc: 50798592 / 65536] to the cell (22, 23) of the portal 0.  A scripted player reads the hero's state in the machine before
each tick and chooses the pad word; nothing is written to the machine after the start.  The binary does NOT run B1 (no roll, no poll): the polls of B1 are the DLL pilot's concern (g4a-route/pilot-rules.md).
Usage: python -B -I g4a_route.py [YD [XJ]]  (defaults 288.0 687.5)  -> out/g4a_route.txt (phases with thresholds, then the per-tick trace)"""
import os
import sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import my392 as G

U = 65536
YD = float(sys.argv[1]) if len(sys.argv) > 1 else 288.0
XJ = float(sys.argv[2]) if len(sys.argv) > 2 else 687.5


class Player:
    def __init__(self, m, S):
        self.m, self.S = m, S
        self.h = G.K.ent(0)
        self.phase = 'down'
        self.last_t = None
        self.last_hold = 0
        self.log = []
        self.jumped = False

    def state(self):
        m, h = self.m, self.h
        return dict(x=m.peeks(h + 0x114) / U, y=m.peeks(h + 0x118) / U, z=m.peeks(h + 0x11C) / U)

    def get(self, t, default=0):
        if self.last_t == t:
            return self.last_hold
        hold = self._get(t)
        self.last_t, self.last_hold = t, hold
        return hold

    def _get(self, t):
        # the transitions are decided from the state read before the tick; the tick holds the direction of the phase it ends in (the rule of the DLL pilot, pilot-rules.md)
        s = self.state()
        ph = self.phase
        hold = 0
        if self.phase == 'south' and s['y'] >= 343.5:
            self.phase = 'west'
        if self.phase == 'west' and s['x'] <= 541.0:
            self.phase = 'down2'
        if self.phase == 'after_jump' and s['x'] <= 600:
            self.phase = 'south'
        if self.phase == 'down' and s['y'] >= YD:
            self.phase = 'left'
        if self.phase == 'left':
            hold = G.LEFT
            if 295.0 <= s['y'] <= 297.0 and s['x'] <= XJ:      # the binary has no poll: the jump is pressed at the first tick inside the window
                hold = G.LEFT | G.CROSS
                self.phase = 'after_jump'
        elif self.phase in ('after_jump', 'west'):
            hold = G.LEFT
        elif self.phase in ('down', 'south', 'down2'):
            hold = G.DOWN
        if self.phase == 'down2' and int(s['x'] // 24) == 22 and int(s['y'] // 16) == 23:
            self.phase = 'done'
            hold = 0
        if self.phase != ph:
            self.log.append((t, ph, self.phase, s['x'], s['y'], s['z']))
        return hold


def main():
    m, S = G.world((775, 216), 0, xraw=int(775.125 * U))
    pl = Player(m, S)
    rows = G.gl.run(m, 400, pl, crates=list(S.values()))
    slot_rec = {v: k for k, v in S.items()}
    out = ['E20.g4a T0 (b) - the route of the JUMP with the 12 records (binary, oracle machine); YD=%.4f XJ=%.4f; start (775.125, 216, z 64)' % (YD, XJ)]
    out.append('phases (a phase starts at the tick given, the thresholds are the conditions that ended the previous phase):')
    for e in pl.log:
        out.append('  tick %3d : %-10s -> %-10s  hero x=%8.3f y=%8.3f z=%7.3f' % e)
    t_done = next((r['tick'] for r in rows if int(r['hero']['px'] / U // 24) == 22 and int(r['hero']['py'] / U // 16) == 23), None)
    H = rows[-1]['hero']
    out.append('first tick with the hero in cell (22, 23): %s ; end (tick 400): x=%.3f y=%.3f z=%.3f' % (t_done, H['px'] / U, H['py'] / U, H['pz'] / U))
    out.append('trace: tick pad x y z anim support xcoll(rec)')
    for r in rows:
        h = r['hero']
        out.append('  t%03d pad=%04X x=%9.4f y=%9.4f z=%7.3f anim=%s sup=%s xcoll=%s' % (
            r['tick'], r['hold'], h['px'] / U, h['py'] / U, h['pz'] / U, h['anim_c'], slot_rec.get(h['support'], '-'), slot_rec.get(h['xcoll'], '-')))
    txt = '\n'.join(out) + '\n'
    open(os.path.join(os.path.dirname(os.path.abspath(__file__)), 'out', 'g4a_route.txt'), 'w', encoding='utf-8').write(txt)
    print('\n'.join(out[:14]))


if __name__ == '__main__':
    main()
