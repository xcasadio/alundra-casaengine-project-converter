"""check_plan_values.py - E20.f6b T0: the table "Valeurs attendues" of docs/plan-e20-heros.md section 1.22.2 and the facts of section 1.22 checked against the keys of the oracle (the frozen keys of ../../bin/out/json and the keys of ./out/json).
Reads only; prints one line per assertion (OK / FAIL) and a summary. Usage (from a COPY of the annex, after run_all.sh): python -B check_plan_values.py > ../t0/check-plan-values.txt
Z of the keys is the binary's (raw 16.16); the DLL's is one raw unit lower (ADR-0026): the plan's table gives the DLL's Z, so pz is compared with +1 raw unit.
"""
import json
import os
import re

HERE = os.path.dirname(os.path.abspath(__file__))
FROZEN = os.path.join(HERE, '..', '..', 'bin', 'out', 'json')
NEW = os.path.join(HERE, 'out', 'json')
results = []


def load(name):
    for d in (FROZEN, NEW):
        p = os.path.join(d, name + '.json')
        if os.path.exists(p):
            return json.load(open(p))
    raise FileNotFoundError(name)


def ball_slot(rows, lo=1):
    for r in rows:
        if r['tick'] < lo:
            continue
        for s, d in r['slots'].items():
            if 10 <= d['type68'] <= 15:
                return s, r['tick']
    raise ValueError('no ball')


def col(rows, slot, tick, c):
    r = rows[tick - 1]
    d = r['slots'].get(slot)
    return None if d is None else d[c]


def check(label, got, want):
    ok = got == want
    results.append(ok)
    print('%-4s %-96s got %s want %s' % ('OK' if ok else 'FAIL', label, got, want))


def life(name, S0, lo=1, status_end=48, free=49, charged=False):
    rows = load(name)
    slot, S = ball_slot(rows, lo)
    check('%s: birth tick S' % name, S, S0)
    sts = [col(rows, slot, S + k, 'status') for k in range(0, free + 1)]
    check('%s: status 2 from S to S+10' % name, sts[0:11], [2] * 11)
    check('%s: status 3 from S+11 to S+%d' % (name, status_end - 1), sts[11:status_end], [3] * (status_end - 11))
    check('%s: status 4 at S+%d, absent at S+%d' % (name, status_end, free), (sts[status_end], sts[free]), (4, None))
    return rows, slot, S


def main():
    # ---- fire11_flat : the table of values --------------------------------------------------------------------------------------------------------------------
    rows, slot, S = life('fire11_flat', 12)
    check('fire11_flat: px 106 at S, +6 per tick, 166 at S+10 and after', [col(rows, slot, S + k, 'px') // 65536 for k in (0, 1, 10, 11, 30)], [106, 112, 166, 166, 166])
    zs = [(col(rows, slot, S + k, 'pz') - 1) / 65536 for k in range(0, 11)]
    check('fire11_flat: DLL Z (binary - 1 unit) S..S+10', [round(z, 4) for z in zs], [31.5, 30.5, 29, 27, 24.5, 21.5, 18, 14, 9.5, 4.5, 0])
    check('fire11_flat: fx 393216 then 0 from S+11', [col(rows, slot, S + k, 'fx') for k in (0, 10, 11)], [393216, 393216, 0])
    check('fire11_flat: fz -32768*(k+1) for k = 0..9 then 0 at S+10', [col(rows, slot, S + k, 'fz') for k in (0, 1, 9, 10, 11)], [-32768, -65536, -327680, 0, 0])
    check('fire11_flat: coll_140 and ground_144 are 1 at S+10', (col(rows, slot, S + 10, 'coll_140'), col(rows, slot, S + 10, 'ground_144')), (1, 1))
    check('fire11_flat: box 30x20x15 in flight, 30x20x31 at hbz 0 from S+11', [tuple(int((col(rows, slot, S + k, c) + 1) / 65536) for c in ('bw', 'bd', 'bh')) for k in (0, 10, 11)], [(30, 20, 15), (30, 20, 15), (30, 20, 31)])
    check('fire11_flat: atk_attr 134, atk_pow 4, flags 0x132154 then 0x132144', (col(rows, slot, S, 'atk_attr'), col(rows, slot, S, 'atk_pow'), hex(col(rows, slot, S, 'flags6C')), hex(col(rows, slot, S + 11, 'flags6C'))),
          (134, 4, '0x132154', '0x132144'))
    check('fire11_flat: reset_AC 1 at S+47', col(rows, slot, S + 47, 'reset_AC'), 1)
    fx = lambda t: rows[t - 1]['effects']
    trail = [(k, e) for k in range(0, 48) for e in fx(S + k) if e.startswith('fx(id=17') and k < 11]
    check('fire11_flat: trail effects 17 at S+4 and S+8 only before the explosion', [k for k, _ in trail], [4, 8])
    check('fire11_flat: trail S+4 (124,100,35) v(-6,0,2); S+8 (148,100,22) v(-6,0,4)', [e for _, e in trail],
          ['fx(id=17,dir=0,x=124.000,y=100.000,z=35.000,v=(-6.0000,0.0000,2.0000))', 'fx(id=17,dir=0,x=148.000,y=100.000,z=22.000,v=(-6.0000,0.0000,4.0000))'])
    check('fire11_flat: explosion at S+11: 10 draws, five effects 17 at (166,100,0), sound 39',
          (rows[S + 11 - 1]['draws'], len([e for e in fx(S + 11) if e.startswith('fx(id=17,dir=0,x=166.000,y=100.000,z=0.000')]), 'snd(39)' in rows[S + 11 - 1]['journal']), (10, 5, True))
    puffs = [k for k in range(12, 50) if rows[S + k - 1]['draws'] == 2]
    check('fire11_flat: puffs (2 draws) at S+16, +24, +32, +40', puffs, [16, 24, 32, 40])
    check('fire11_flat: puff effect 17 at Z 8 (0 + 0x80000) at (166, 100)', fx(S + 16)[0].split(',v=')[0], 'fx(id=17,dir=0,x=166.000,y=100.000,z=8.000')
    check('fire11_flat: the puff vz is 2 at all four puffs', [fx(S + k)[0].endswith(',2.0000))') for k in (16, 24, 32, 40)], [True] * 4)
    check('fire11_flat: no puff at the death tick S+48 (no effect, no draw)', (rows[S + 48 - 1]['draws'], [e for e in fx(S + 48) if e.startswith('fx(')]), (0, []))
    # ---- ice -------------------------------------------------------------------------------------------------------------------------------------------------
    rows, slot, S = life('ice14_flat', 12, status_end=44, free=45)
    check('ice14_flat: x = 103 at S, 109 at S+1, 163 at S+10', [col(rows, slot, S + k, 'px') // 65536 for k in (0, 1, 10)], [103, 109, 163])
    check('ice14_flat: effect 18 trail (121,100,27) at S+4 and (145,100,14) at S+8, no Z offset', (rows[S + 4 - 1]['effects'][0].split(',v=')[0], rows[S + 8 - 1]['effects'][0].split(',v=')[0]),
          ('fx(id=18,dir=0,x=121.000,y=100.000,z=27.000', 'fx(id=18,dir=0,x=145.000,y=100.000,z=14.000'))
    check('ice14_flat: sound 38 at the explosion S+11', 'snd(38)' in rows[S + 11 - 1]['journal'], True)
    check('ice14_flat: reset_AC 1 at S+43', col(rows, slot, S + 43, 'reset_AC'), 1)
    # ---- charged ---------------------------------------------------------------------------------------------------------------------------------------------
    for name, S0, end, free, sound, power in (('fire12_flat', 107, 48, 49, 41, 12), ('ice15_flat', 107, 68, 69, 40, 12)):
        rows = load(name)
        slot = [s for s, d in rows[S0 - 1]['slots'].items() if 10 <= d['type68'] <= 15][0]
        sts = [col(rows, slot, S0 + k, 'status') for k in range(0, free + 1)]
        check('%s: statuses 2 (S..S+10), 3 (S+11..S+%d), 4 at S+%d, absent at S+%d' % (name, end - 1, end, free), (sts[0:11] == [2] * 11, sts[11:end] == [3] * (end - 11), sts[end], sts[free]), (True, True, 4, None))
        check('%s: S = R + 11 = 107 (R = 96), atk_pow %d, sound %d at S+11' % (name, power, sound), (col(rows, slot, S0 + 11, 'atk_pow'), 'snd(%d)' % sound in rows[S0 + 11 - 1]['journal']), (power, True))
        box = [int((col(rows, slot, S0 + 11, c) + 1) / 65536) for c in ('bw', 'bd', 'bh')]
        check('%s: explosion box 36x24x31' % name, box, [36, 24, 31])
    rows = load('ice15_flat')
    slot = [s for s, d in rows[107 - 1]['slots'].items() if 10 <= d['type68'] <= 15][0]
    check('ice15_flat: puffs until S+64', [k for k in range(12, 70) if rows[107 + k - 1]['draws'] == 2][-1], 64)
    check('ice15_flat: reset_AC 1 at S+67', col(rows, slot, 107 + 67, 'reset_AC'), 1)
    # ---- wall, steps, water -------------------------------------------------------------------------------------------------------------------------------------
    rows = load('fire11_wall_col6')
    slot, S = ball_slot(rows)
    flag = [k for k in range(0, 15) if col(rows, slot, S + k, 'adj_13C') == 1]
    check('fire11_wall_col6: the wall flag (adj_13C) at S+6, explosion (10 draws) at S+7 with the five effects at Z 18 (the ball Z before the physics of the tick), landing S+10',
          (flag[0], rows[S + 7 - 1]['draws'], sorted(set(e.split('z=')[1].split(',')[0] for e in rows[S + 7 - 1]['effects'])), col(rows, slot, S + 10, 'coll_140')), (6, 10, ['18.000'], 1))
    check('fire11_wall_col6: the next puff comes at S+8 (the counter 8 is a multiple of 8), 2 draws, Z 22', (rows[S + 8 - 1]['draws'], [e.split('z=')[1].split(',')[0] for e in rows[S + 8 - 1]['effects']]), (2, ['22.000']))
    rows = load('fire11_step16_col6')
    slot, S = ball_slot(rows)
    check('fire11_step16_col6: lands on the step at S+7, explosion S+8, death S+45, free S+46', (col(rows, slot, S + 7, 'coll_140'), rows[S + 8 - 1]['draws'], col(rows, slot, S + 45, 'status'), col(rows, slot, S + 46, 'status')), (1, 10, 4, None))
    rows = load('ice14_step16_col6')
    slot, S = ball_slot(rows)
    check('ice14_step16_col6: death S+41, free S+42', (col(rows, slot, S + 41, 'status'), col(rows, slot, S + 42, 'status')), (4, None))
    rows = load('fire11_step32_col6')
    slot, S = ball_slot(rows)
    check('fire11_step32_col6: like the wall: flag at S+6, explosion S+7, landing S+10', (col(rows, slot, S + 6, 'adj_13C'), rows[S + 7 - 1]['draws'], col(rows, slot, S + 10, 'coll_140')), (1, 10, 1))
    for name in ('fire11_gp8_deepwater', 'ice14_gp8_deepwater', 'fire11_gp2_slope1'):
        rows = load(name)
        slot, S = ball_slot(rows)
        got = (col(rows, slot, S + 11, 'status'), col(rows, slot, S + 12, 'status'), sum(r['draws'] for r in rows[S - 1:S + 20]), [e for e in rows[S + 11 - 1]['effects'] if e.startswith('attach')])
        check('%s: dies at S+11 by the pick phase / E9 (attach(6)), free at S+12, no draw' % name, (got[0], got[1], got[2], len(got[3]), got[3][0].startswith('attach(id=6')), (4, None, 0, 1, True))
    for name in ('fire11_gp4_slope2', 'fire11_gp6_slope3', 'fire11_mixed_footprint_water'):
        rows = load(name)
        slot, S = ball_slot(rows)
        check('%s: normal explosion (status 3, 10 draws, sound 39) at S+11' % name, (col(rows, slot, S + 11, 'status'), rows[S + 11 - 1]['draws'], 'snd(39)' in rows[S + 11 - 1]['journal']), (3, 10, True))
    rows = load('fire11_wall_then_water')
    slot, S = ball_slot(rows)
    check('fire11_wall_then_water: explosion at S+7, then destroyed at S+11 by the branch A (attach 6)', (rows[S + 7 - 1]['draws'], col(rows, slot, S + 11, 'status'), any(e.startswith('attach(id=6') for e in rows[S + 11 - 1]['effects'])), (10, 4, True))
    # ---- touches -----------------------------------------------------------------------------------------------------------------------------------------------------
    rows = load('fire11_vs_target')
    attach = [(r['tick'], e) for r in rows for e in r['effects'] if e.startswith('attach(')]
    slot, S = ball_slot(rows)
    check('fire11_vs_target: the target is touched in flight at S+8: attach(4) before the sparks, 9 draws, the ball goes on', (attach[0][0] - S, attach[0][1][:11], rows[attach[0][0] - 1]['draws']), (8, 'attach(id=4', 9))
    rows = load('ice14_vs_target')
    attach = [(r['tick'], e) for r in rows for e in r['effects'] if e.startswith('attach(')]
    slot, S = ball_slot(rows)
    check('ice14_vs_target: touched at S+9: attach(5)', (attach[0][0] - S, attach[0][1][:11]), (9, 'attach(id=5'))
    # ---- the three flames of the map 309 ----------------------------------------------------------------------------------------------------------------------
    for name in ('flames309', 'flames309_real'):
        rows = load(name)
        emit = [r['tick'] for r in rows if r['effects']]
        check('%s: effect 17 every 4 ticks from tick 5, three per emission, (x,y,Z 40) v 0' % name,
              (emit[:5], sorted(set(e.split(',v=')[1] for r in rows if r['effects'] for e in r['effects'])), len(rows[4]['effects']), sorted(e.split(',x=')[1].split(',z=')[0] for e in rows[4]['effects'])),
              ([5, 9, 13, 17, 21], ['(0.0000,0.0000,0.0000))'], 3, ['936.000,y=664.000', '948.000,y=672.000', '960.000,y=664.000']))
        check('%s: z of the effects 40, no draw, the flames stay at status 2 with flags 0x132104' % name, (set(e.split('z=')[1].split(',')[0] for r in rows if r['effects'] for e in r['effects']), sum(r['draws'] for r in rows), set(hex(d['flags6C']) for r in rows for d in r['slots'].values()), set(d['status'] for r in rows for d in r['slots'].values())),
              ({'40.000'}, 0, {'0x132104'}, {2}))
    rows = load('flames309_without_A_program')
    slot = list(rows[10]['slots'])[0]
    check('flames309_without_A_program (the control): the ball explodes on the ground', any(d['status'] == 3 for r in rows for d in r['slots'].values()), True)
    print('%d of %d assertions OK' % (sum(results), len(results)))


if __name__ == '__main__':
    main()
