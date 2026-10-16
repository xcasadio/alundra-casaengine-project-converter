"""run_hit_effect.py - O-F3-6: the hit effect 4 / 5 on the MIPS machine with the REAL effect creation 0x8003BE74 / 0x8003BDD8, the REAL hit pass 0x80039300, the REAL entity-effects
pass 0x80038E84 and the REAL effect-slot pass 0x8003C410 (animation step 0x8003BBDC), in the order of the frame function 0x8002E058.

usage: python -B run_hit_effect.py            (writes out/*.txt and out/*.json next to this file)

Scenarios (all: 'HIT' tick T = 8):
  hero_hits_bean   : the hero is the attacker, the Beannoid (slot 1) the target (as hitfx.py of the f3 annex)
  bean_hits_hero   : the Beannoid is the attacker, the hero (slot 0) the target (as the DLL test (w1): Alundra.Tests/AlundraHitEffectWorldTests.cs)
For nibbles 6, 10 (effect 4) and 7, 9 (effect 5) and, as a control, 1 (no effect).
Order 'binary'         : [eight passes of 0x8003B388 with the real 0x80038E84 and 0x80039300] then the slot pass 0x8003C410 (0x8002E108, 0x8002E110).
Order 'slotpass_first' : COUNTERFACTUAL = the executor's assumption (the slot pass of tick T runs BEFORE the hit pass of tick T) : not the binary's order.

The attack is injected between the passes 0x80038E84 and 0x80039300 of tick T (the same poke as hitfx.trial: attack record, frame box flag, hit box = the target's box, counters cleared,
response byte of the target sheet for the nibble set to 0x10 = not immune), and removed right after the hit pass, so that exactly one hit happens.
"""
import sys
import os
import json

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import fxlib as F      # noqa: E402
import world as W      # noqa: E402
import pworld as P     # noqa: E402

OUT = os.path.join(HERE, 'out')
os.makedirs(OUT, exist_ok=True)

ATK = 0x801F8000
T_HIT = 8
N_AFTER = 110
EFFECT_OF_NIBBLE = {6: 4, 10: 4, 7: 5, 9: 5, 1: None}
HERO = P.ENT0
BEAN = P.ENT0 + P.STRIDE * 1


def inject(m, attacker, target, nib, resp=0x10):
    saved = {}
    for off in (0x1D4, 0x1C8, 0x6C, 0x1FC, 0x200, 0x204):          # +0x220 (hit counter) is NOT restored: it is the evidence that the pass registered the hit
        saved[(attacker, off)] = m.peek(attacker + off)
    m.poke8(ATK, nib if nib else 0x80)
    m.poke8(ATK + 1, 1)
    m.poke16(ATK + 2, 0)
    m.poke(attacker + 0x1D4, 1)
    m.poke(attacker + 0x1C8, ATK)
    if attacker == HERO:
        m.poke(attacker + 0x6C, m.peek(attacker + 0x6C) | 4)        # hitfx.trial: hero_flags_or = 4
    for src, dst in ((0x1D8, 0x1FC), (0x1DC, 0x200), (0x1E0, 0x204)):
        m.poke(attacker + dst, m.peek(target + src))                # the hit box is placed on the target's box
    m.poke(attacker + 0x220, 0)
    m.poke(target + 0x1D0, 0)
    m.poke(target + 0x1CC, 0)
    m.poke(target + 0x224, 0)
    sheet = m.peek(target + 0x1C4)
    saved['resp'] = (sheet + 2 + nib, m.peek(sheet + 2 + nib) & 0xFF)
    m.poke8(sheet + 2 + nib, resp)
    return saved


def restore_attacker(m, saved):
    for k, v in saved.items():
        if k == 'resp':
            continue
        m.poke(k[0] + k[1], v)
    # the attack is over: no frame box, no attack record (the hit already happened)
    # (hit counter +0x220 and target counters keep the values the pass wrote)


def drawn(m, slot):
    """0x8003C4A8-0x8003C4B8: the slot pass appends (slot + 0xC) to the display list [0x80126E6C] for every active slot it did not free"""
    n, p, lst = F.display_entries(m)
    return (F.slot_addr(slot) + 0xC) in lst


def run(scenario, nib, order='binary', t_hit=T_HIT, n_after=N_AFTER, resp=0x10):
    m, meta = F.build()
    pad = W.Pad()
    attacker, target = (HERO, BEAN) if scenario == 'hero_hits_bean' else (BEAN, HERO)
    want_fx = EFFECT_OF_NIBBLE[nib]
    rows = []
    tracked = None
    created = None
    sparks_seen = {}
    total = t_hit + n_after
    for t in range(1, total + 1):
        m.tick_no = t
        row = dict(tick=t, d=t - t_hit)
        m.poke(F.DISPLAY_PTR, F.DISPLAY_LIST)         # 0x8002E0F0-0x8002E104
        m.poke(F.DISPLAY_COUNT, 0)
        if order == 'slotpass_first':
            W.run_pass(m, 0x8003C410)
            row['pre_slot_pass'] = True
        F.pad_tick_prefix(m, pad)
        saved = None
        for a in W.PASSES:
            if a == 0x80039300 and t == t_hit:
                saved = inject(m, attacker, target, nib, resp)
            before = set(F.active_slots(m)) if t == t_hit and a == 0x80039300 else None
            W.run_pass(m, a)
            if a == 0x80039300 and t == t_hit:
                restore_attacker(m, saved)
                new = [i for i in F.active_slots(m) if i not in before]
                row['hit_pass_new_slots'] = [(i, F.slot_read(m, i)['id'], F.slot_read(m, i)['mode'], F.slot_read(m, i)['status'], F.slot_read(m, i)['counter'], F.slot_read(m, i)['done']) for i in new]
                for i in new:
                    s = F.slot_read(m, i)
                    if want_fx is not None and s['id'] == want_fx and s['anchor'] == target and tracked is None:
                        tracked = i
                        created = dict(tick=t, pass_addr=a, slot=i, snap=s)
                    if s['id'] == 9:
                        sparks_seen[i] = dict(tick=t, snap=s)
        if order == 'binary':
            W.run_pass(m, 0x8003C410)
        # record the state at the END of the tick (what the DLL test (w1) reads after arena.Frame)
        if tracked is not None:
            s = F.slot_read(m, tracked)
            ordn, word = F.frame_ordinal(m, s)
            row.update(slot=tracked, status=s['status'], mode=s['mode'], anchor_ok=(s['anchor'] == target), id=s['id'], counter=s['counter'], done=s['done'],
                       seq_cur=s['seq_cur'], frame_ordinal=ordn, image_word=word, drawn=drawn(m, tracked), x=s['x'], depth_off=s['depth_off'])
        row['spark_slots'] = [(i, F.slot_read(m, i)['status'], F.slot_read(m, i)['counter'], F.slot_read(m, i)['done']) for i in sparks_seen]
        row['active_ids'] = sorted((i, F.slot_read(m, i)['id'], F.slot_read(m, i)['status']) for i in F.active_slots(m))
        row['hit_counter_attacker'] = m.peek(attacker + 0x220)
        row['target_status'] = m.peek(target + 0x10)
        row['target_c1d0'] = m.peeks(target + 0x1D0)
        row['target_touching'] = m.peek(target + 0x224)
        row['target_hp'] = m.peeks(target + 0x14)
        rows.append(row)
    # ---- summary
    res = dict(scenario=scenario, nibble=nib, order=order, response_byte=resp, hit_registered=any(r['hit_counter_attacker'] >= 1 for r in rows if r['tick'] == t_hit),
               target_touching_at_T=[r['target_touching'] for r in rows if r['tick'] == t_hit][0], target_c1d0_at_T=[r['target_c1d0'] for r in rows if r['tick'] == t_hit][0], want_effect=want_fx, T=t_hit, created=created and dict(tick=created['tick'], slot=created['slot'], id=created['snap']['id'], mode=created['snap']['mode'],
               status=created['snap']['status'], anchor_is_target=(created['snap']['anchor'] == target), depth_off=created['snap']['depth_off'], sel=created['snap']['sel'], seq_target=created['snap']['seq_target']))
    if tracked is not None:
        seen = [r for r in rows if r.get('status')]
        first_seen = seen[0]['tick']
        absent = [r for r in rows if r['tick'] > first_seen and r.get('status') == 0]
        first_absent = absent[0]['tick'] if absent else None
        drawn_ticks = [r['tick'] for r in rows if r.get('drawn')]
        done_ticks = [r['tick'] for r in rows if r.get('done')]
        res.update(first_seen_tick=first_seen, first_seen_d=first_seen - t_hit, first_absent_tick=first_absent, first_absent_d=(first_absent - t_hit) if first_absent else None,
                   drawn_first_d=drawn_ticks[0] - t_hit if drawn_ticks else None, drawn_last_d=drawn_ticks[-1] - t_hit if drawn_ticks else None, drawn_count=len(drawn_ticks),
                   done_flag_first_d=done_ticks[0] - t_hit if done_ticks else None)
    # sparks (effect 9) : same questions
    sp = {}
    for i, info in sparks_seen.items():
        st = [(r['tick'], [x for x in r['spark_slots'] if x[0] == i][0][1]) for r in rows if r['tick'] >= info['tick']]
        fa = [t for t, s in st if s == 0]
        sp[i] = dict(created_tick=info['tick'], first_absent_d=(fa[0] - t_hit) if fa else None)
    res['sparks'] = sp
    res['uninit_reads_effect_path'] = sorted({'%08X' % k[0] for k in m.uninit_reads if 0x8003B9C4 <= k[0] < 0x8003C500 or 0x80038E84 <= k[0] < 0x80039190})
    return res, rows, meta


def dump_rows(path, res, rows, lo, hi):
    with open(path, 'w') as f:
        f.write('# scenario=%s nibble=%d order=%s  T(hit tick)=%d  want effect=%s\n' % (res['scenario'], res['nibble'], res['order'], res['T'], res['want_effect']))
        f.write('# created (in the hit pass): %s\n' % json.dumps(res['created']))
        f.write('# columns: tick d | END-of-tick state of the tracked slot: status(2=active,0=free) mode counter(+0x7c) done(+0x7d) seq_cur(+0x71) frame_ordinal image_word drawn | attacker hit counter, target status/hp\n')
        for r in rows:
            if r['d'] < lo or r['d'] > hi:
                continue
            if 'status' in r:
                f.write('tick %3d d=%+4d | status=%d mode=%d counter=%3d done=%d seq=%3d frame#=%2s word=%3s drawn=%s | atk_hit=%d tgt_status=%d tgt_hp=%d | new_in_hit_pass=%s\n' % (
                    r['tick'], r['d'], r['status'], r['mode'], r['counter'], r['done'], r['seq_cur'], r['frame_ordinal'], r['image_word'], r['drawn'],
                    r['hit_counter_attacker'], r['target_status'], r['target_hp'], r.get('hit_pass_new_slots', '')))
            else:
                f.write('tick %3d d=%+4d | (no tracked slot yet) active=%s | atk_hit=%d | new_in_hit_pass=%s\n' % (r['tick'], r['d'], r['active_ids'], r['hit_counter_attacker'], r.get('hit_pass_new_slots', '')))
        f.write('# summary: %s\n' % json.dumps({k: v for k, v in res.items() if k != 'created'}))


def main():
    summary = []
    for scenario in ('hero_hits_bean', 'bean_hits_hero'):
        for nib in (6, 10, 7, 9, 1):
            for order in ('binary', 'slotpass_first'):
                if order == 'slotpass_first' and nib not in (6, 7):
                    continue
                res, rows, meta = run(scenario, nib, order)
                name = '%s_nib%d_%s' % (scenario, nib, order)
                dump_rows(os.path.join(OUT, name + '.txt'), res, rows, -2, 101)
                json.dump(dict(result=res, rows=rows), open(os.path.join(OUT, name + '.json'), 'w'))
                summary.append(res)
                print('%-34s created=%s first_absent_d=%s drawn=%s..%s (%s ticks) done_flag_d=%s sparks=%s' % (
                    name, (res['created'] or {}).get('tick'), res.get('first_absent_d'), res.get('drawn_first_d'), res.get('drawn_last_d'), res.get('drawn_count'), res.get('done_flag_first_d'),
                    {k: v['first_absent_d'] for k, v in res['sparks'].items()}))
    for scenario in ('hero_hits_bean', 'bean_hits_hero'):          # control: an immune response (resp & 0xC0 == 0x80) attaches nothing (0x800395E8 beq -> 0x8003966C)
        res, rows, meta = run(scenario, 6, 'binary', resp=0x80)
        name = '%s_nib6_immune_binary' % scenario
        dump_rows(os.path.join(OUT, name + '.txt'), res, rows, -2, 5)
        json.dump(dict(result=res, rows=rows), open(os.path.join(OUT, name + '.json'), 'w'))
        summary.append(res)
        print('%-34s hit_registered=%s effect4_created=%s new slots in hit pass=%s' % (name, res['hit_registered'], res['created'] is not None, [r.get('hit_pass_new_slots') for r in rows if r['tick'] == T_HIT]))
    json.dump(summary, open(os.path.join(OUT, 'summary.json'), 'w'), indent=1)


if __name__ == '__main__':
    main()
