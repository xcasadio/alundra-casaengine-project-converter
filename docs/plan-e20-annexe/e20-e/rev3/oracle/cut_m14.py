"""cut_m14.py - E20.e rev3 T0 (g), (g2), (h): the oracle of the world tests `cut_m14`.

The hero ALONE on the REAL cells of map 14 (data-extracted/data/map_14.json: walkability, ground property, slope, height byte of the 3 120 cells), real code of
ALUN_CD.EXE on the machine of the E20 oracles; the generator word moves once per logical tick (O-E-1, ADR-0066: `ticker_with_step`).  Scenarios:

  sword_none   the basic sword, Square pressed at tick 1, hero at (732, 696) facing west, a seed s0 that gives NO object at any cut
  wave_none    the Steel Edge held ticks 1..95, released at 96, hero at (744, 696) facing west (the wave is born at tick 117), seed s0
  nostack_none the basic sword at (708, 728) facing west: the cell (27, 45) has no stack of tiles (Q-E4), seed s0
  sword_obj    as sword_none with a seed whose FIRST cut (tick 2) gives an object; stops 3 ticks after the birth
  wave_obj     as wave_none with a seed whose first cut of the WAVE gives an object (the hero's own cuts give none); stops 3 ticks after the birth
  nograss      the basic sword in the air far from any grass (map 14, hero (212, 96) facing east): the generator tick by tick (g2)

The seeds are found by the model of ChooseRandomlyAnItem (cutlib.choose) over the schedule of the cuts, then CHECKED against the real calls (rng_before/rng_after/ret of each real
0x80032C7C call).  Output: out/cut_m14.json (+ out/cut_m14.txt, a reading aid).  Nothing here knows an expected value of the DLL.
"""
import json
import os
import re
import sys

import cutlib as L

OUT = L.OUT
WEAPON_TYPES = tuple(L.P.PROJECTILE_TYPES) + (99, 100, 101, 114, 115)     # the five objects a cut can drop (item + 0x1E): their sprite records in the machine

SCENARIOS = {
    'sword_none': dict(map=14, hero=(732, 696), direction='west', wave=False, n=40),
    'wave_none': dict(map=14, hero=(744, 696), direction='west', wave=True, n=150),
    'nostack_none': dict(map=14, hero=(708, 728), direction='west', wave=False, n=40),
    'nograss': dict(map=14, hero=(212, 96), direction='east', wave=False, n=40),
    'beside': dict(map=14, hero=(744, 696), direction='east', wave=False, n=40),
    'idle': dict(map=14, hero=(732, 696), direction='west', wave=False, n=40, press=False),
}


def run(sc, seed, n=None, types=WEAPON_TYPES):
    log = L.Log()
    holds = {t: L.SQUARE for t in range(1, 96)} if sc['wave'] else ({1: L.SQUARE} if sc.get('press', True) else {})
    spec = dict(item=2 if sc['wave'] else None, n=n or sc['n'], holds=holds, after=0, hero_px=sc['hero'], direction=L.DIRS[sc['direction']], cells=L.cells_fn(sc['map']),
                seed=seed, init=[L.fill_extra(sc['map']), L.install_spies(log, True)], ticker=L.ticker_with_step, types=types)
    return L.P.run(spec), log


def schedule(res, log):
    """[(tick, caller slot)] one entry per cut, in order, from the spies"""
    sched = []
    for c in log.cate:
        sched += [(c['tick'], c['slot'])] * c['cuts']
    return sched


def steps_between(a, b, limit=12):
    """number of generator steps from state a to state b (None if more than limit)"""
    k = 0
    while a != b:
        a = L.rng_step(a)
        k += 1
        if k > limit:
            return None
    return k


def extras_of(res, log):
    """{tick: number of generator steps that are neither the tick's own step nor a draw of a cut}: the hold sparks of the charge (UpdatePlayerWeaponEffect draws two numbers per spark)"""
    ex = {}
    prev = res['rng0'] & L.M32
    cutdraws = {}
    for c in log.loot:
        cutdraws[c['tick']] = cutdraws.get(c['tick'], 0) + steps_between(c['rng_before'], c['rng_after'])
    for r in res['rows']:
        k = steps_between(prev, r['rng'] & L.M32)
        assert k is not None, r['tick']
        e = k - 1 - cutdraws.get(r['tick'], 0)
        if e:
            assert r['tick'] not in cutdraws, 'a spark draw and a cut in the same tick: the order inside the tick would matter'
            ex[r['tick']] = e
        prev = r['rng'] & L.M32
    return ex


def model_items(sched, n, seed, extras=None):
    """replay the generator over a schedule: -> list of (tick, caller slot, item, draws, state_before, state_after) per cut"""
    s = seed
    out = []
    by_tick = {}
    for t, slot in sched:
        by_tick.setdefault(t, []).append(slot)
    for t in range(1, n + 1):
        for slot in by_tick.get(t, []):
            before = s
            item, draws, s = L.choose(0xFF, s)
            out.append((t, slot, item, draws, before, s))
        for _ in range((extras or {}).get(t, 0)):
            s = L.rng_step(s)
        s = L.rng_step(s)
    return out


def find_seed(sched_list, want, start=0x1000, limit=200000):
    """want(list of model_items per scenario) -> bool.  sched_list = [(sched, n, extras)]"""
    for seed in range(start, start + limit):
        if want([model_items(s, n, seed, ex) for s, n, ex in sched_list]):
            return seed
    raise RuntimeError('no seed')


def describe(res, log, sc, seed, name):
    rows = res['rows']
    cuts = []
    loot_i = 0
    cate_iter = []
    for c in log.cate:
        cate_iter += [c] * c['cuts']
    # one fx per cut in the journal order; the loot call of the cut is the loot_i-th real call (the call order of CATE is cut -> fx -> loot -> sound)
    for r in rows:
        for j in r['journal']:
            m = re.match(r'fx\(id=(\d+),a2=(\d+),x=([-\d.]+),y=([-\d.]+),z=([-\d.]+)\)', j)
            if not m or int(m.group(1)) != 2:      # the other effects (the charge burst, 0x10) are not cuts
                continue
            L_ = log.loot[loot_i]
            c = cate_iter[loot_i]
            assert L_['tick'] == r['tick'] == c['tick']
            fx = dict(id=int(m.group(1)), a2=int(m.group(2)), x=L_['x'], y=L_['y'], z=L_['z'])
            assert abs(float(m.group(3)) * 65536 - L_['x']) < 1 and abs(float(m.group(4)) * 65536 - L_['y']) < 1 and abs(float(m.group(5)) * 65536 - L_['z']) < 1
            col = (L_['x'] - 0xC0000) // 0x180000
            row = (L_['y'] - 0x80000) // 0x100000
            item, draws, after = L.choose(0xFF, L_['rng_before'])
            cuts.append(dict(tick=r['tick'], caller='hero' if c['slot'] == 0 else 'wave', slot=c['slot'], col=col, row=row, effect=fx,
                             loot=dict(code=L_['code'], rng_before=L_['rng_before'], rng_after=L_['rng_after'], ret=L_['ret'], model_item=item, model_draws=draws,
                                       model_after=after, model_matches=(after == L_['rng_after'])),
                             height=L.map_tiles(sc['map'])[row * 52 + col]['Height'] if 0 <= row < 60 and 0 <= col < 52 else None))
            loot_i += 1
    assert loot_i == len(log.loot) == len(cate_iter), (loot_i, len(log.loot), len(cate_iter))
    # the order of the effects, objects and sounds, tick by tick
    sequence = []
    for r in rows:
        seq = []
        for j in r['journal']:
            if j.startswith('fx(') or j.startswith('snd('):
                seq.append(j)
        if seq:
            sequence.append(dict(tick=r['tick'], events=seq))
    # the objects that were born: an entity of the loot types that was not there at the previous tick
    born = []
    seen = set(rows[0]['slots'].keys()) if rows else set()
    for r in rows[1:]:
        for s_, d in r['slots'].items():
            if s_ not in seen and d['type68'] in (99, 100, 101, 114, 115):
                born.append(dict(tick=r['tick'], slot=s_, type=d['type68'], x=d['px'], y=d['py'], z=d['pz'], state_b274=d['b274'], life_b278=d['b278']))
        seen = set(r['slots'].keys())
    # (h) P-E1: what the machine does with each born object in the ticks that follow its birth: its position (the physics pass moves it), and the handlers the events pass calls for its slot
    first_run = []
    for b in born:
        per = []
        for r in rows:
            if b['tick'] <= r['tick'] <= b['tick'] + 3:
                d = r['slots'].get(b['slot'])
                hs = [j for j in r['journal'] if j.startswith('stub') and j.endswith('(slot%d)' % b['slot'])]
                per.append(dict(tick=r['tick'], pz=d['pz'] if d else None, ground=d['ground_144'] if d else None, force_z=d['fz'] if d else None, handlers=hs))
        first_handler = next((p['tick'] for p in per if p['handlers']), None)
        first_run.append(dict(slot=b['slot'], birth=b['tick'], first_handler_tick=first_handler, ticks=per))
    hero_states = []
    last = None
    for r in rows:
        a = r['slots'][0]['anim_t']
        if a != last:
            hero_states.append(dict(tick=r['tick'], anim_t=a))
            last = a
    # cells after: every cut cell, as the machine's RAM holds it
    m = res['m']
    cells_after = []
    tiles = L.map_tiles(sc['map'])
    for k in cuts:
        c, r_ = k['col'], k['row']
        if 0 <= c < 52 and 0 <= r_ < 60:
            w = L.cell_words(m, c, r_)
            t = tiles[r_ * 52 + c]
            cells_after.append(dict(col=c, row=r_, before=dict(flags=t['Walkability'] | (t['GroundProperty'] << 8), height=t['Height'], wall_word=t['WallTilesOffset'] & 0xFFFF,
                                                                   has_stack=bool(t['WallTiles'])),
                                    after=dict(flags=w['flags'], height=w['h'], wall_word=w['wall'])))
    return dict(name=name, map=sc['map'], hero_px=list(sc['hero']), direction=sc['direction'], weapon='steel-held' if sc['wave'] else 'basic', seed=seed, n=len(rows),
                rng0=res['rng0'] & L.M32, rng_by_tick=[r['rng'] & L.M32 for r in rows], hero_states=hero_states, cuts=cuts, sequence=sequence, born=born, object_first_ticks=first_run, cells_after=cells_after,
                objects_dropped=sum(1 for k in cuts if k['loot']['ret'] != 0))


def main():
    os.makedirs(OUT, exist_ok=True)
    # 1. the schedules (seed-independent: an object born at a cut does not move any later cut)
    scheds = {}
    for name, sc in SCENARIOS.items():
        res, log = run(sc, 0x12345678)
        scheds[name] = (schedule(res, log), sc['n'], extras_of(res, log))
    # 2. s0: no object at any cut of the three grass scenarios
    s0 = find_seed([scheds[k] for k in ('sword_none', 'wave_none', 'nostack_none')], lambda ms: all(it == 0 for m in ms for (_, _, it, _, _, _) in m))
    # 3. s_sword_obj: the first cut (tick 2) gives an object
    sched_sw = scheds['sword_none']
    s_sw = find_seed([sched_sw], lambda ms: ms[0][0][2] != 0)
    # 4. s_wave_obj: the cuts of the hero give nothing; the first cut of the wave gives an object
    sched_wv = scheds['wave_none']

    def want_wave(ms):
        m = ms[0]
        hero = [x for x in m if x[1] == 0]
        wave = [x for x in m if x[1] != 0]
        return all(x[2] == 0 for x in hero) and bool(wave) and wave[0][2] != 0
    s_wv = find_seed([sched_wv], want_wave)
    out = dict(binary='ALUN_CD.EXE (France) sha1 a705f8dab665645af5dfaf8fb348c7926b5caa58', generator_step_per_tick=True, seeds=dict(s0=s0, sword_obj=s_sw, wave_obj=s_wv), scenarios={})
    lines = []
    for name, sc in SCENARIOS.items():
        res, log = run(sc, s0)
        d = describe(res, log, sc, s0, name)
        out['scenarios'][name] = d
    # the object scenarios: stop 3 ticks after the birth
    for name, base, seed in (('sword_obj', 'sword_none', s_sw), ('wave_obj', 'wave_none', s_wv)):
        sc = dict(SCENARIOS[base])
        res, log = run(sc, seed)
        first_obj = [c for c in log.loot if c['ret'] != 0]
        assert first_obj, name
        birth = first_obj[0]['tick']
        res, log = run(sc, seed, n=birth + 3)
        out['scenarios'][name] = describe(res, log, sc, seed, name)
    # every real loot call agrees with the model
    bad = [(n, k['tick']) for n, d in out['scenarios'].items() for k in d['cuts'] if not k['loot']['model_matches'] or (k['loot']['model_item'] != 0) != (k['loot']['ret'] != 0)]
    out['model_vs_real_mismatches'] = bad
    json.dump(out, open(os.path.join(OUT, 'cut_m14.json'), 'w'), indent=1, sort_keys=True)
    # reading aid
    for name, d in out['scenarios'].items():
        lines.append('# %s: map %d hero %s %s %s seed %08X, %d ticks, %d cuts, %d objects born' % (name, d['map'], d['hero_px'], d['direction'], d['weapon'], d['seed'], d['n'], len(d['cuts']), len(d['born'])))
        lines.append('  hero states: ' + ' '.join('t%d:%X' % (h['tick'], h['anim_t']) for h in d['hero_states']))
        for k in d['cuts']:
            lines.append('  t%03d %-4s cell(%d,%d) h=%s fx(id=%d x=%.1f y=%.1f z=%.1f) loot rng %08X -> %08X ret=%d model(item=%d draws=%d)' % (
                k['tick'], k['caller'], k['col'], k['row'], k['height'], k['effect']['id'], k['effect']['x'] / 65536, k['effect']['y'] / 65536, k['effect']['z'] / 65536,
                k['loot']['rng_before'], k['loot']['rng_after'], k['loot']['ret'], k['loot']['model_item'], k['loot']['model_draws']))
        for s in d['sequence']:
            lines.append('  order t%03d: %s' % (s['tick'], ' '.join(s['events'])))
        for fr in d['object_first_ticks']:
            lines.append('  object slot %d born t%d: first handler call at t%s; per tick %s' % (fr['slot'], fr['birth'], fr['first_handler_tick'], [(p['tick'], p['pz'], p['handlers']) for p in fr['ticks']]))
        for b in d['born']:
            lines.append('  born t%03d slot %d type %d at (%.1f, %.1f, rawZ %d)' % (b['tick'], b['slot'], b['type'], b['x'] / 65536, b['y'] / 65536, b['z']))
    lines.append('model vs real mismatches: %s' % out['model_vs_real_mismatches'])
    open(os.path.join(OUT, 'cut_m14.txt'), 'w').write('\n'.join(lines) + '\n')
    print('\n'.join(lines))
    print('seeds', {k: '%08X' % v for k, v in out['seeds'].items()})
    return 1 if bad else 0


if __name__ == '__main__':
    sys.exit(main())
