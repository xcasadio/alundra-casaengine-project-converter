"""heroattack_m10.py - E20.e rev3 T0 (d): the existing test `HeroAttackTests.RealPrefab_TheHeroOfTheExport_SwingsTheBasicSwordOnTheGroundAndInTheAir_AtTheTicksOfTheOracle`
(Alundra.Tests/Hero/HeroAttackTests.cs, map 10 'Overworld 2,1-10', arrival pixel (252, 936) = cell (10, 58), direction 16) swings the basic sword on the ground (Square at its tick 1, 21 ticks), waits,
jumps (Cross) and swings in the air (Square 4 ticks later) and asserts the sounds `[29]` for each swing.  Does the binary cut grass there, at which tick, with which sounds and which generator words?
The pad is the test's: Square at tick 1, Cross at tick 27, Square at tick 31 (frames of one tick: the press frame, 21 more, 4 idle, the jump frame, 3 more, the air press, 10 more).
Real code on the machine of the E20 oracles, the real cells of map 10, a seed s0 (any: the draws are recorded).  Writes out/heroattack_m10.json and .txt.
"""
import json
import os

import cutlib as L

OUT = L.OUT
SEED = 0x1002


def main():
    log = L.Log()
    holds = {1: L.SQUARE, 27: L.W.CROSS, 31: L.SQUARE}
    spec = dict(item=None, n=45, holds=holds, after=0, hero_px=(252, 936), direction=16, cells=L.cells_fn(10), seed=SEED, init=[L.fill_extra(10), L.install_spies(log, True)],
                ticker=L.ticker_with_step, types=tuple(L.P.PROJECTILE_TYPES) + (99, 100, 101, 114, 115))
    res = L.P.run(spec)
    rows = res['rows']
    states = []
    last = None
    for r in rows:
        a = r['slots'][0]['anim_t']
        if a != last:
            states.append((r['tick'], a))
            last = a
    cuts = []
    for c in log.loot:
        cuts.append(dict(tick=c['tick'], col=(c['x'] - 0xC0000) // 0x180000, row=(c['y'] - 0x80000) // 0x100000, z=c['z'], rng_before=c['rng_before'], rng_after=c['rng_after'], ret=c['ret']))
    sounds = [(r['tick'], j) for r in rows for j in r['journal'] if j.startswith('snd(')]
    calls = [dict(tick=c['tick'], cuts=c['cuts'], box=c['box']) for c in log.cate if c['slot'] == 0]
    out = dict(map=10, hero_px=[252, 936], pad='Square@1 Cross@27 Square@31', seed=SEED, hero_states=states, cuts=cuts, sounds=sounds, rng_by_tick=[r['rng'] & L.M32 for r in rows],
               calls_made=len(calls), hero_pz_min_max=[min(r['slots'][0]['pz'] for r in rows), max(r['slots'][0]['pz'] for r in rows)])
    json.dump(out, open(os.path.join(OUT, 'heroattack_m10.json'), 'w'), indent=1, sort_keys=True)
    lines = ['hero states (tick, state): %s' % states, 'CATE calls made: %d' % len(calls), 'cuts: %s' % cuts, 'sounds: %s' % sounds, 'hero pz min/max: %s' % out['hero_pz_min_max']]
    open(os.path.join(OUT, 'heroattack_m10.txt'), 'w').write('\n'.join(lines) + '\n')
    print('\n'.join(lines))


if __name__ == '__main__':
    main()
