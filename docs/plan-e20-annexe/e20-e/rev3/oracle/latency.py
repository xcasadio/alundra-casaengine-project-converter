"""latency.py - E20.e rev3 T0 (c): the box CheckAndTriggerTileEffect reads is the box stored at the END of the previous tick (0x80038064 from the physics pass 0x800384BC,
or at the creation 0x80039F30), not the box of the frame shown in the tick of the call.  Machine of the E20 oracles, real code.
For every call of the hero (basic sword, map 14 scenario sword_none) and of the wave (scenario wave_none): the box read at the call, against the box stored at the end of tick t-1 and at the end of tick t.
Writes out/latency.json and out/latency.txt.
"""
import json
import os

import cut_m14 as M
import cutlib as L

OUT = L.OUT


def box_of(d):
    return (d['hbx'], d['hby'], d['hbz'], d['bw'], d['bd'], d['bh'], 1 if d['box1d4'] else 0)


def analyse(name):
    sc = M.SCENARIOS[name]
    res, log = M.run(sc, 0x1002)
    rows = {r['tick']: r for r in res['rows']}
    out = dict(scenario=name, calls=0, equals_end_of_previous_tick=0, equals_end_of_this_tick=0, differs_between_the_two=0, equals_previous_where_they_differ=0, equals_this_where_they_differ=0, examples=[])
    for c in log.cate:
        t, slot = c['tick'], c['slot']
        if t - 1 not in rows or slot not in rows[t - 1]['slots'] or slot not in rows[t]['slots']:
            # first call of an entity born in this very tick: its box was stored at the creation, no previous tick
            out.setdefault('born_this_tick', []).append(dict(tick=t, slot=slot))
            continue
        call = (c['box']['x'], c['box']['y'], c['box']['z'], c['box']['w'], c['box']['d'], c['box']['h'], 1 if c['box']['frame'] else 0)
        prev = box_of(rows[t - 1]['slots'][slot])
        this = box_of(rows[t]['slots'][slot])
        out['calls'] += 1
        out['equals_end_of_previous_tick'] += call == prev
        out['equals_end_of_this_tick'] += call == this
        if prev != this:
            out['differs_between_the_two'] += 1
            out['equals_previous_where_they_differ'] += call == prev
            out['equals_this_where_they_differ'] += call == this
            if len(out['examples']) < 6:
                out['examples'].append(dict(tick=t, slot=slot, call=call, end_prev=prev, end_this=this))
    return out


def main():
    res = [analyse('sword_none'), analyse('wave_none')]
    json.dump(res, open(os.path.join(OUT, 'latency.json'), 'w'), indent=1, sort_keys=True)
    lines = []
    for r in res:
        lines.append('%s: %d calls; box == end of the previous tick: %d; == end of this tick: %d; ticks where the two stored boxes differ: %d (call == previous: %d, call == this: %d); born in the call tick: %s' % (
            r['scenario'], r['calls'], r['equals_end_of_previous_tick'], r['equals_end_of_this_tick'], r['differs_between_the_two'], r['equals_previous_where_they_differ'],
            r['equals_this_where_they_differ'], r.get('born_this_tick')))
        for e in r['examples']:
            lines.append('   t%d slot %d call %s | end t-1 %s | end t %s' % (e['tick'], e['slot'], e['call'], e['end_prev'], e['end_this']))
    open(os.path.join(OUT, 'latency.txt'), 'w').write('\n'.join(lines) + '\n')
    print('\n'.join(lines))


if __name__ == '__main__':
    main()
