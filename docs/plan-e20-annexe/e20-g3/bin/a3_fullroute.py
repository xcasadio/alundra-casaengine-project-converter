"""a3_fullroute.py - disc-e20g3 A3: from the story's arrival ledge (720, 216, z=64: B1 of map 392 places the hero there) to the exit portal cell (22, 23), through the barricade, in the real tick
(real geometry; crates rec4..rec8, rec9, rec10, rec11 from the real spawner; real MovePlayer).  A scripted 'player' reads the hero's state in the machine before each tick and chooses the pad word:
  JUMP : RIGHT off the ledge, DOWN n_d ticks (n_d = 49: y = 295.62, inside the window), LEFT until x <= 692, LEFT+CROSS, LEFT until x <= 600, then DOWN / LEFT / DOWN to the portal cell.
  LIFT : same approach, LEFT until the contact, LEFT+SQUARE (lift), wait the gesture, LEFT until x <= 600 carrying the crate, SQUARE (throw), then DOWN / LEFT / DOWN to the portal cell.
Nothing is written to the machine after the start.  Output out/a3_fullroute.txt"""
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import lib392 as L

OUT = []
def P(s):
    OUT.append(s); print(s, flush=True)

class Player:
    def __init__(self, m, kind, nd=49):
        self.m, self.kind, self.nd = m, kind, nd
        self.h = L.K.ent(0)
        self.phase = 'right'
        self.cnt = 0
        self.log = []

    def state(self):
        m, h = self.m, self.h
        return dict(x=m.peeks(h + 0x114) / 65536, y=m.peeks(h + 0x118) / 65536, z=m.peeks(h + 0x11C) / 65536, anim=m.peeks(h + 0x88), xcoll=m.peek(h + 0x130), carried=m.peek(h + 0x2C))

    def get(self, t, default=0):
        if getattr(self, 'last_t', None) == t:        # gl.run asks twice per tick (once for the tick, once for its row)
            return self.last_hold
        h = self._get(t)
        self.last_t, self.last_hold = t, h
        return h

    def _get(self, t):
        s = self.state()
        ph = self.phase
        hold = 0
        if ph == 'right':
            hold = L.RIGHT
            if s['x'] >= 752:
                self.phase, self.cnt = 'down', 0
                hold = L.DOWN
        if self.phase == 'down':
            hold = L.DOWN
            self.cnt += 1
            if self.cnt >= self.nd:
                self.phase = 'left'
        elif self.phase == 'left':
            hold = L.LEFT
            if self.kind == 'jump' and s['x'] <= 692:
                hold = L.LEFT | L.CROSS
                self.phase = 'after_jump'
            if self.kind == 'lift' and s['xcoll']:
                hold = L.LEFT | L.SQUARE
                self.phase = 'gesture'
        elif self.phase == 'after_jump':
            hold = L.LEFT
            if s['x'] <= 600:
                self.phase, self.cnt = 'south', 0
        elif self.phase == 'gesture':
            hold = 0
            if s['anim'] in (12, 7, 6):
                self.phase = 'carry'
                hold = L.LEFT
        elif self.phase == 'carry':
            hold = L.LEFT
            if s['x'] <= 600:
                hold = L.SQUARE
                self.phase, self.cnt = 'thrown', 0
        elif self.phase == 'thrown':
            hold = 0
            self.cnt += 1
            if self.cnt >= 12:
                self.phase = 'south'
        if self.phase == 'south':
            hold = L.DOWN
            if s['y'] >= 343.5:
                self.phase = 'west'
        elif self.phase == 'west':
            hold = L.LEFT
            if s['x'] <= 541:
                self.phase = 'down2'
        elif self.phase == 'down2':
            hold = L.DOWN
            if s['y'] >= 376:
                self.phase = 'done'
        elif self.phase == 'done':
            hold = 0
        if self.phase != ph:
            self.log.append((t, ph, self.phase, round(s['x'], 2), round(s['y'], 2), round(s['z'], 2)))
        return hold


def run(kind):
    m, S = L.world392(hero_px=(720, 216), direction=0, rec_ids=(4, 5, 6, 7, 8, 9, 10, 11))
    pl = Player(m, kind)
    rows = L.gl.run(m, 400, pl, crates=list(S.values()))
    P('== %s route' % kind.upper())
    for e in pl.log:
        P('  tick %3d : phase %-10s -> %-10s  hero x=%7.2f y=%7.2f z=%6.2f' % e)
    H = rows[-1]['hero']
    x, y = H['px'] / 65536, H['py'] / 65536
    P('  end (tick 400): hero x=%.2f y=%.2f z=%.2f cell=(%d, %d) ; portal cell (22, 23) ; exit window x in [538, 541], y in [375, 376] (plan-e19-opcodes.md:2142-2151)' % (x, y, H['pz'] / 65536, int(x // 24), int(y // 16)))
    t_done = next((r['tick'] for r in rows if int(r['hero']['px'] / 65536 // 24) == 22 and int(r['hero']['py'] / 65536 // 16) == 23), None)
    P('  first tick with the hero in cell (22, 23): %s' % t_done)
    return rows, S

for k in ('jump', 'lift'):
    run(k)
open(os.path.join(L.OUT, 'a3_fullroute.txt'), 'w', encoding='utf-8').write('\n'.join(OUT) + '\n')
