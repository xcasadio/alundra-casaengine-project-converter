"""e14scen.py - E14.a1 oracle: the scenario list.  SETUP and INPUTS only; no expected value (the readers in e14out.py print what the emulated binary did).

Units: record positions are map entity record fields (X = 12 * XPos + 12 px, Y = 8 * YPos + 8 px, Z = Height << 19); the hero is placed in px; a tile is 24 x 16 px (TileX = x // 24, TileY = y // 16).
The hero is 'ghost' (+0x1CC kept at 0x78 before every tick) unless a spec says otherwise: the hit pass never lets a claw reach him, so the hero's own state (E20.c2) does not enter the traces.
Swing timing (E20.c1 H1, reproduced here): Square at tick k, first contact at tick k + 4 for a target 40 px east of the hero (hero facing east).
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.join(HERE, 'tools'))
import e14run as R
import world as W

SQ = W.SQUARE
RIGHT, DOWN, UP, LEFT = W.RIGHT, W.DOWN, W.UP, W.LEFT

TX, TY = 252, 96            # px of the map entity record (20, 11, 0) used by the flat-field scenarios (as E20.c1)


def flat_rec(x=20, y=11, h=0):
    return dict(x=x, y=y, h=h)


def spec(name, desc, kind, recs, hero_px, n, **kw):
    d = dict(name=name, desc=desc, kind=kind, recs=recs, hero_px=hero_px, n=n, map=None, direction=24, seed=0x12345678, holds={}, after=0, before={}, ghost=True, probes=[],
             frame_advance=False, group='')
    d.update(kw)
    return d


def add_before(before, tick, *evs):
    before.setdefault(tick, []).extend(evs)


def kill_plan(slot, start, hits=2, gap=30, dx=40, holds=None, before=None):
    """scripted sword hits on the enemy in `slot`: at tick `start` and every `gap` ticks the hero is put dx px west of it (forces zeroed) and presses Square;
       the hit lands 4 ticks after each press.  Returns (holds, before) (updated copies)."""
    holds = dict(holds or {})
    before = {k: list(v) for k, v in (before or {}).items()}
    for k in range(hits):
        t = start + gap * k
        add_before(before, t, R.ev_hero_beside(slot, dx))
        holds[t] = SQ
    return holds, before


def scenarios():
    L = []
    # ---------------------------------------------------------------- Beannoide, flat field (map-14 constants), record (20, 11, 0) at (252, 96) px, tile (10, 6)
    L.append(spec('BN-dormant', 'Beannoide asleep: hero 200 px east (8 tiles); program A at status 1 (A1: anim 2), then C1 every tick in animation 2 with nothing in reach', 'beannoide',
                  [flat_rec()], (452, 96), 150, group='beannoide'))
    L.append(spec('BN-wake-walk', 'hero 3 tiles west (TileX diff 3, TileY diff 0): C1 case 2 wakes it (anim 4, 95 ticks), idle 30, then it is NOT within 2 tiles so it walks (case 0 walk path)',
                  'beannoide', [flat_rec()], (180, 96), 380, group='beannoide'))
    L.append(spec('BN-lunge', 'hero 2 tiles west (204, 96): wake (anim 4), idle, then within reach (rel < 3, floor diff <= 0x200000): anim 3 (notice + lunge, 93 ticks), then StartWalk(1, 0x28, 0x14)',
                  'beannoide', [flat_rec()], (204, 96), 460, group='beannoide'))
    b = {}
    add_before(b, 2, R.ev_state(1, anim=0, ai1=0))
    L.append(spec('BN-walk-free', 'awake (harness writes anim 0, AIValues[1] 0 before tick 2), hero 250 px away: C1 case 0 walk path (anim 1, 60..120 ticks), free expiry (case 1, ForceAdjusted 0: random idle and '
                  'random direction), repeated; flat field, no wall', 'beannoide', [flat_rec()], (452, 96), 700, before=b, group='beannoide'))
    b = {}
    add_before(b, 2, R.ev_state(1, anim=1, direction=8, ai1=100))
    L.append(spec('BN-walk-blocked', 'map 116 (real cells), record 0 at (32, 36, 20) = px (396, 296), tile (16, 18), the room of the grate: harness writes anim 1 (walk), direction 8 (west), AIValues[1] 100 before '
                  'tick 2; the wall of column 14/15 stops it (ForceAdjusted), the expiry at tick 101 takes the BLOCKED branch (quantizer table, forces zeroed, anim not written)',
                  'beannoide', [dict(x=32, y=36, h=20)], (732, 648), 260, map=116, before=b, group='beannoide'))
    L.append(spec('BN-hurt', 'one sword hit on the sleeping Beannoide (hero 40 px west, Square at tick 1, contact at tick 5): D1 at tick 6 (ComputeNewHp 9 -> 7, anim 5, direction away), then C1 case 7 at the end of '
                  'the hurt animation', 'beannoide', [flat_rec()], (212, 96), 120, holds={1: SQ}, group='beannoide'))
    holds, before = kill_plan(1, 1, hits=5, gap=30)
    L.append(spec('BN-death', 'five sword hits (Square at 1, 31, 61, 91, 121; the hero is put 40 px west of the Beannoide before each swing): HP 9 -> 7 -> 5 -> 3 -> 1 -> 0; C1 case 7 sets anim 6 and Flags |= 0x40, '
                  'then E1 (Deactivate trigger) destroys it; 0x2C [0] is read before every tick', 'beannoide', [flat_rec()], (212, 96), 200, holds=holds, before=before, probes=[0], group='beannoide'))
    # ---------------------------------------------------------------- Muruta, flat field
    L.append(spec('MU-calm', 'Muruta calm: hero 400 px east (out of every cone); A0 at tick 1, then C10 state 0 calm: a new sequence starts with RNG(4) == 1; hops (anim 8), cardinal turn, quarter turn, walk (anim 12)',
                  'muruta', [flat_rec()], (652, 96), 600, group='muruta'))
    L.append(spec('MU-alert-close', 'Muruta (facing down, AnimationDirection 0) with the hero 1 tile south (252, 112): front cone -> alerted (Bytes[0] 1); close branch RNG(8) -> hops, walk away, claw hop (anim 7), '
                  'retreat hop (anim 4)', 'muruta', [flat_rec()], (252, 112), 300, group='muruta'))
    L.append(spec('MU-alert-mid', 'hero 4 tiles south (252, 160): front cone, rel1 = 4: the MID branch RNG(5)', 'muruta', [flat_rec()], (252, 160), 300, group='muruta'))
    L.append(spec('MU-alert-far', 'hero 6 tiles south (252, 192): front cone, rel1 = 6: the FAR branch RNG(8)', 'muruta', [flat_rec()], (252, 192), 300, group='muruta'))
    holds = {t: SQ for t in range(10, 300, 30)}
    L.append(spec('MU-lunge', 'alerted Muruta, hero 3 tiles south swinging the sword (frame box + attack attribute while Square: swings at 10, 40, 70, ...): the lunge branch (rel < 5): anim 3 toward the hero',
                  'muruta', [flat_rec()], (252, 144), 300, holds=holds, group='muruta'))
    L.append(spec('MU-hurt', 'one sword hit (hero 40 px west, Square at tick 1, contact at tick 5): D9 at tick 6 (ComputeNewHp 3 -> 1, anim 2, direction away), anim 5 hold, C10 state 5 recovery '
                  '(Bytes[0] 1, anim 0, AIValues[1] 30)', 'muruta', [flat_rec()], (212, 96), 120, holds={1: SQ}, group='muruta'))
    holds, before = kill_plan(1, 1, hits=2, gap=30)
    L.append(spec('MU-death', 'two sword hits (Square at 1 and 31, hero put 40 px west before each): 3 -> 1 -> 0; D9 sets Bytes[3] 1, C10 state 5 sets anim 6 and Flags |= 0x40, then E1; 0x2C [0] read before every tick',
                  'muruta', [flat_rec()], (212, 96), 100, holds=holds, before=before, probes=[0], group='muruta'))
    # ---------------------------------------------------------------- Muruta, hall of map 115 (real cells), records 24..27, native from the start (the C[25] hand-over already done), Height 0 (landed)
    hall = [None] * 24 + [dict(x=18, y=92, h=0), dict(x=24, y=92, h=0), dict(x=18, y=100, h=0), dict(x=24, y=100, h=0)]
    L.append(spec('MU-hall-calm', 'hall of map 115, real cells, the four Muruta of records 24-27 landed (Height 0) and native; hero at the portal side, tile (10, 55) = px (252, 888); 300 ticks; 0x2C [24..27] read before every tick',
                  'muruta', hall, (252, 888), 300, map=115, probes=[24, 25, 26, 27], group='hall'))
    L.append(spec('MU-hall-kill', 'hall of map 115: the four Muruta are killed in turn by a sword policy (R.Swordsman: the hero is put 40 px west of the current target and presses Square whenever the target is '
                  'hittable, 25 ticks apart); 0x2C [24..27] read before every tick: the hall wait loop ends when all four counts are zero', 'muruta', hall, (252, 888), 700, map=115,
                  controller=R.Swordsman([1, 2, 3, 4], dxs=(40, 34, 28), mapnum=115), probes=[24, 25, 26, 27], group='hall'))
    grate = [dict(x=32, y=36, h=20), dict(x=40, y=36, h=20)]
    L.append(spec('BN-grate-kill', 'grate room of map 116 (real cells): the Beannoide records 0 and 1 at (32, 36, 20) and (40, 36, 20), hero in the room at tile (18, 20) = px (444, 328); both wake (anim 4); '
                  'a sword policy (from tick 120) kills them: the hero is put 40 / 34 / 28 / 22 / 16 px west of the current target, the first spot whose cells are floor at the height of the target; 0x2C [0] and [1] read before every tick', 'beannoide', grate, (444, 328), 900, map=116,
                  controller=R.Swordsman([1, 2], dxs=(40, 34, 28, 22, 16), start=120, mapnum=116), probes=[0, 1], group='grate'))
    # the same deaths with the binary's per-frame RNG advance (0x8002BD14): the stream the original really has; the timeline must not depend on it
    holds, before = kill_plan(1, 1, hits=2, gap=30)
    L.append(spec('MU-death-adv', 'MU-death with the per-frame RNG advance applied after every tick (s = s * 0x7D2B89DD + 0xE06A02E7, the three instructions at 0x8002BD14-0x8002BD34)', 'muruta', [flat_rec()],
                  (212, 96), 100, holds=holds, before=before, probes=[0], frame_advance=True, group='muruta'))
    holds, before = kill_plan(1, 1, hits=5, gap=30)
    L.append(spec('BN-death-adv', 'BN-death with the per-frame RNG advance applied after every tick', 'beannoide', [flat_rec()], (212, 96), 200, holds=holds, before=before, probes=[0], frame_advance=True,
                  group='beannoide'))
    # ---------------------------------------------------------------- the E20.c1 arena with the natives live (what the E14.a0 switch has to keep off)
    L.append(spec('ARENA-H1-live', 'E20.c1 H1 (hero 40 px west of the Beannoide, Square at tick 1, 40 ticks) with the LIVE handlers: the Beannoide is asleep (A1: anim 2), the sword reaches it', 'beannoide',
                  [flat_rec()], (212, 96), 40, holds={1: SQ}, group='arena'))
    L.append(spec('ARENA-H3-live', 'E20.c1 H3 (hero 60 px west, Right held from tick 1, 60 ticks) with the LIVE handlers and a NON-ghost hero: the Beannoide wakes (anim 4) as the hero comes within 3 tiles, its claws only exist '
                  'on the last frame of the wake animation', 'beannoide', [flat_rec()], (192, 96), 60, after=RIGHT, ghost=False, group='arena'))
    return L


def by_name(name):
    for s in scenarios():
        if s['name'] == name:
            return s
    raise KeyError(name)
