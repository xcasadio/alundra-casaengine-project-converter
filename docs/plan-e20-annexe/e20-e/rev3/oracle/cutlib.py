"""cutlib.py - E20.e rev3 T0: shared helpers of the oracle of CheckAndTriggerTileEffect (0x8003A7B0).

REAL code of ALUN_CD.EXE (France) on the E20.c1 / f-projectiles machine (tools/pworld.py and its siblings, byte copies of docs/plan-e20-annexe/e20-f3/oracle/tools).
What is real: the hero (InitializeEntity, MovePlayer), the animation and physics passes, the wave (C61/E10, SpawnWarpEntity), CheckAndTriggerTileEffect itself, and
SpawnRandomLoot 0x80032C7C with ChooseRandomlyAnItem 0x80032968 and the pseudo-random word [0x80098708].  What is a journalling stub: CreateEffectEntity 0x8003BDD8,
PlaySoundEffect 0x800490FC, the effects pass 0x80038E84, every handler other than the projectile ones.
Nothing here knows an expected value of the DLL.  Run from a COPY (the scripts write into ./out next to themselves): python -B <script>.py

The pseudo-random word moves once per logical tick in the binary (frame function 0x8002BAEC ends with one step, ADR-0066, e20-e0b); the machine's tick (W.tick) does not
include that step, so `ticker_with_step` adds it after the passes of every tick (O-E-1).
"""
import json
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, 'tools'))
import pworld as P            # noqa: E402
import world as W             # noqa: E402
import c1world as C           # noqa: E402
from machine import s32, M32  # noqa: E402

DATA = r"D:/development/repo/alundra-casaengine-project-converter/data-extracted/data"
RNG = C.RNG
MUL, INC = 0x7D2B89DD, 0xE06A02E7
CATE = 0x8003A7B0
LOOT = 0x80032C7C
OUT = os.path.join(HERE, 'out')
ENT0, STRIDE = P.ENT0, P.STRIDE

DIRS = {'east': 24, 'north': 16, 'west': 8, 'south': 0}
SQUARE = W.SQUARE


def rng_step(s):
    return (s * MUL + INC) & M32


# ------------------------------------------------------------------ the real cells of a map
_MAPS = {}


def map_tiles(mapno):
    if mapno not in _MAPS:
        d = json.load(open('%s/map_%d.json' % (DATA, mapno), encoding='utf-8-sig'))
        _MAPS[mapno] = d['Map']['MapTiles']
    return _MAPS[mapno]


def cells_fn(mapno):
    """the cells(col,row) -> (height byte, slope, walkability, ground property) of build_world, from the decoded map"""
    t = map_tiles(mapno)

    def fn(c, r):
        x = t[r * 52 + c]
        return (x['Height'] & 0xFF, x['Slope'] & 0xFF, x['Walkability'] & 0xFF, x['GroundProperty'] & 0xFF)
    return fn


def fill_extra(mapno):
    """init hook: the tile id (+4) and the wall-stack word (+6) of every cell, so that the cut's write of 0xFFFF at +6 is observable"""
    t = map_tiles(mapno)

    def init(m):
        for r in range(60):
            for c in range(52):
                a = W.TILES + 0x604 + r * W.ROW + c * 8
                x = t[r * 52 + c]
                m.poke16(a + 4, x['TileId'] & 0xFFFF)
                m.poke16(a + 6, x['WallTilesOffset'] & 0xFFFF)
    return init


def cell_words(m, c, r):
    a = W.TILES + 0x604 + r * W.ROW + c * 8
    return dict(flags=m.rh(a), h=m.rb(a + 3), tile=m.rh(a + 4), wall=m.rh(a + 6))


# ------------------------------------------------------------------ spies
class Log:
    def __init__(self):
        self.loot = []
        self.cate = []


def install_spies(log, want_cate=True):
    def init(m):
        def loot_spy(mm):
            a = [mm.r[4], mm.r[5], mm.r[6], mm.r[7]]
            before = mm.peek(RNG) & M32
            h = mm.hooks.pop(LOOT)
            sp = ((mm.r[29] - 0x800) & ~7)
            v0 = C.side_call(mm, LOOT, a, sp)
            mm.hooks[LOOT] = h
            after = mm.peek(RNG) & M32
            log.loot.append(dict(tick=mm.tick_no, code=a[0] & 0xFF, x=s32(a[1]), y=s32(a[2]), z=s32(a[3]), rng_before=before, rng_after=after, ret=v0 & M32))
            mm.r[2] = v0
        m.hooks[LOOT] = loot_spy
        if want_cate:
            def cate_spy(mm):
                a0 = mm.r[4]
                ra = mm.r[31]
                slot = (a0 - ENT0) // STRIDE
                box = dict(x=mm.peeks(a0 + 0x1FC), y=mm.peeks(a0 + 0x200), z=mm.peeks(a0 + 0x204), w=mm.peeks(a0 + 0x214), d=mm.peeks(a0 + 0x218), h=mm.peeks(a0 + 0x21C),
                           frame=mm.peek(a0 + 0x1D4))
                j0 = len(mm.journal)
                h = mm.hooks.pop(CATE)
                sp = ((mm.r[29] - 0x800) & ~7)
                v0 = C.side_call(mm, CATE, [a0], sp)
                mm.hooks[CATE] = h
                cuts = [j for j in mm.journal[j0:] if j[1] == 'effect_3BDD8']
                log.cate.append(dict(tick=mm.tick_no, slot=slot, ra=ra, box=box, cuts=len(cuts)))
                mm.r[2] = v0
            m.hooks[CATE] = cate_spy
    return init


def ticker_with_step(m, pad, hold):
    """one logical tick of the binary: the passes of 0x8003B388, then the step of the shared generator that ends the frame function"""
    W.tick(m, pad, hold)
    m.poke(RNG, rng_step(m.peek(RNG) & M32))


# ------------------------------------------------------------------ one scenario
def run_scenario(mapno, hero_px, direction, wave, n, seed, step=True, want_cate=True, extra_init=()):
    """wave False: the basic sword (the starting weapon), Square pressed at tick 1 only.  wave True: the Steel Edge (item 2), Square held ticks 1..95, released at 96."""
    log = Log()
    holds = {t: SQUARE for t in range(1, 96)} if wave else {1: SQUARE}
    spec = dict(item=2 if wave else None, n=n, holds=holds, after=0, hero_px=hero_px, direction=DIRS[direction], cells=cells_fn(mapno), seed=seed,
                init=[fill_extra(mapno), install_spies(log, want_cate)] + list(extra_init))
    if step:
        spec['ticker'] = ticker_with_step
    res = P.run(spec)
    return res, log


def cut_events(res, log):
    """one event per cut, in the order of the journal: the effect (id, position), then the loot call of the same call, then the sounds of the tick in order"""
    events = []
    loot_by_tick = {}
    for L in log.loot:
        loot_by_tick.setdefault(L['tick'], []).append(L)
    for r in res['rows']:
        seq = []
        for j in r['journal']:
            m = re.match(r'fx\(id=(\d+),a2=(\d+),x=([-\d.]+),y=([-\d.]+),z=([-\d.]+)\)', j)
            if m:
                seq.append(('fx', int(m.group(1)), int(m.group(2)), float(m.group(3)), float(m.group(4)), float(m.group(5))))
            elif j.startswith('snd('):
                seq.append(('snd', int(j[4:-1])))
            elif j.startswith('fxattach') or j.startswith('stub'):
                continue
        events.append((r['tick'], seq, r))
    return events


# ------------------------------------------------------------------ the loot model (ChooseRandomlyAnItem 0x80032968), checked against the real code by loot_check.py
def loot_table():
    sys.path.insert(0, os.path.join(HERE, 'tools'))
    import lib
    o = 0x80028E0C - lib.BASE + lib.OFF
    return lib.DATA[o:o + 2048]


_TABLE = None


def choose(code, state):
    """-> (item, draws, state_after): one step of the generator per chain link, then table[(code & 0x7F) * 16 + (state >> 28)]"""
    global _TABLE
    if _TABLE is None:
        _TABLE = loot_table()
    draws = 0
    while True:
        if code >= 0x100:
            return 0, draws, state
        if code & 0x80:
            state = rng_step(state)
            draws += 1
            code = _TABLE[(code & 0x7F) * 16 + (state >> 28)]
            continue
        return (code if code < 0x62 else 0), draws, state
