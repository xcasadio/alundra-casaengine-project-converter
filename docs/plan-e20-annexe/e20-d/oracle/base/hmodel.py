"""hmodel.py - E20.c2-0: INDEPENDENT model of the hurt hero, used only to cross-check the real code run by the emulated machine.

Written from docs/plan-e20-heros.md 1.11 (task T4) and the reports c2-binary.md / c2-oracle.md / check.md of the discovery, NOT from the emulated code and not from the disassembly
of MovePlayer: it never reads a machine, only (a) the INPUTS of a run (its spec: inventory, stats, events, flags, pad words, cell word) and (b) the geometry / counter columns of
the rows (positions, directions, slope, the toucher of the previous tick, its attack bytes, the target kinds).  Shared with the oracle: the item tables and the balance blob read
from the disc through kmodel.py (as in E20.c1b), and kmodel's sheet combination.

The state machine (one step per logic tick, MovePlayer first, then the other passes, END of MovePlayer in the middle):

  tick gate     [0x800DC4B8] & 0x48 -> only the lists pass runs: hero +0x1C, +0x1CC, position, HP, the stats and the regeneration counters stay as they were, the hit pass does not run
                (TouchingEntity stays as it was); the draw list is recomputed from the frozen +0x1CC.
  path          flags & 0x34 -> locked: the damage entry only when the whole word is exactly 0x20 (no animation, direction or sound change); otherwise nothing; flags & 0x34 == 0 -> normal.
  damage entry  a toucher (TouchingEntity of the previous tick's hit pass) -> ComputeNewHp on the hero sheet, direction = GetDirection(hero - toucher);
                else (+0x1CC == 0, boots < 3, hazard cost n = (VramOR & 0x180) >> 7 != 0) -> HP = HP - cost (0 if cost >= HP), direction = (+0x8C + 16) & 31.
  hurt          normal path: animation 0x3A when the slope read at the start of the tick is 4, else 0x31; direction; +0x1CC = 0x78; locked 0x20: +0x1CC = 0x78 only.
  counters      +0x1CC decrements by one if non-zero, after the events pass of the same tick -> 0x77 on the hurt tick.
  lists         the hero is in the draw list iff status in {2, 3} and (+0x1CC & 3) != 3.
  END           regeneration (gates: flags & 0x7C == 0, pad hold word 0, HP != 0, HP < HpMax; per record with a non-zero byte 2: counter + 1, HP + 1 when byte 2 < counter, counter 0),
                then the stats mirror: HpMax' = clamp(HpMax, 0, 50), HP' = clamp(HP, 0, HpMax').
"""
import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, 'tools'))
import kmodel as KM
import scen as S
import hworld as H

HAZARD_COST = (0, 1, 2, 3)


def clamp_max(v):
    return 50 if v >= 0x33 else (0 if v < 0 else v)


def clamp_hp(v, smax):
    return smax if smax < v else (0 if v < 0 else v)


def dir_model(vx, vy):
    """direction 0..31 of the vector (vx, vy) = hero - toucher: 0 = down (+Y), 8 = left, 16 = up, 24 = right.  Returns (direction, margin to the nearest sector boundary in sector units)"""
    if vx == 0 and vy == 0:
        return None, 0.0
    a = math.atan2(-vx, vy) * 16.0 / math.pi
    k = math.floor(a + 0.5)
    return k & 31, 0.5 - abs(a - k)


class Inputs:
    """what a run's spec says, in the model's terms"""
    def __init__(self, spec):
        self.spec = spec
        inv = H.INV[spec.get('inv', 'kit')]
        self.inv = KM.Inventory(inv['owned'], inv['weapon'], inv['item'])
        self.recs = KM.model_records(self.inv, 0)
        self.boots = KM.model_boots(KM.Inventory(inv['owned'], inv['weapon'], inv['item']))
        if self.recs[0]:
            base = KM.blob_slice(self.recs[0], 255)
        else:
            base = KM.blob()[1024:1024 + 255]            # the hero's own record (balance index 0, FileOffset 1024 in BALANCE.BIN.json)
        self.sheet = KM.model_sheet(self.recs, base)
        self.periods = [KM.blob()[r[0] + 2] if r else 0 for r in self.recs]
        smax = clamp_max(spec.get('hpmax', 10))
        self.hp0, self.hpmax0 = clamp_hp(spec.get('hp', 10), smax), smax
        self.events = spec.get('events', {})
        self.every = spec.get('flags_every_tick')
        self.holds = spec.get('holds', {})
        self.after = spec.get('after', 0)
        self.cell = spec.get('cells_word', 0)


def toucher_attack(res, t, slot):
    """(attribute, power) of the toucher in slot `slot` at the end of tick t - 1 (row t - 2 of rows, or pre) - its CurrentAttack bytes at the start of tick t"""
    prev = res['pre'] if t == 1 else res['rows'][t - 2]
    k = res['meta']['target_slots'].index(slot)
    tg = prev['targets'][k]
    return tg['atk_attr'], tg['atk_power'], tg


def damage(inp, attr, power, hp):
    """ResolveBalanceTarget rule on the hero sheet (c1b K2): the equipment term only for attribute bit 0x80 (no hurting attack has it; asserted by the caller)"""
    resp = lambda n: inp.sheet[n + 2]
    new, d, how = KM.damage_model(attr, power, resp, hp, [None, None, None], 0)
    return new, d


def check(res):
    """-> list of (rule, ok, detail).  res: a run result of hworld.run."""
    sp = res['spec']
    inp = Inputs(sp)
    rows = res['rows']
    out = []

    def rule(name, ok, detail=''):
        out.append((name, bool(ok), detail))

    bad = {}

    def note(name, t, what):
        bad.setdefault(name, []).append('t%d %s' % (t, what))

    # ---- static: boots and sheet on every row (pre included)
    for r in [res['pre']] + rows:
        g = r['g']
        if g['boots'] != inp.boots:
            note('boots', r['tick'], 'boots %d model %d' % (g['boots'], inp.boots))
        for i in range(1, 12):
            if g['sheet_r%d' % i] != inp.sheet[2 + i]:
                note('sheet', r['tick'], 'r%d %02X model %02X' % (i, g['sheet_r%d' % i], inp.sheet[2 + i]))
    # ---- the state machine
    e_hp, e_max = inp.hp0, inp.hpmax0
    s_hp, s_max = inp.hp0, inp.hpmax0
    c1cc = 0
    cnt = [0, 0, 0]
    flags = 0
    dead = False
    hurts = []                      # (tick, 'normal'|'locked', anim)
    gated_ticks = set()
    for t in range(1, len(rows) + 1):
        row = rows[t - 1]
        prev = res['pre'] if t == 1 else rows[t - 2]
        ph = prev['hero']
        if inp.every is not None:
            flags = inp.every
        else:
            flags = prev['g']['ctl_flags']
        for ev in inp.events.get(t, ()):
            if ev[0] == 'flags':
                flags = ev[1]
            elif ev[0] == 'stats_hp':
                s_hp = clamp_hp(ev[1], s_max)
            elif ev[0] == 'stats_hpmax':
                s_max = clamp_max(ev[1])
            elif ev[0] == 'vessel':
                e_max += 1
                e_hp = e_max
            elif ev[0] == 'hero_hp':
                e_hp = ev[1]
        hold = inp.holds.get(t, inp.after)
        h = row['hero']
        gb = row['g']
        if (gb['stats_hp_before'], gb['stats_hpmax_before']) != (s_hp, s_max):
            note('stats', t, 'stats before the tick %s model %s' % ((gb['stats_hp_before'], gb['stats_hpmax_before']), (s_hp, s_max)))
        if (gb['ent_hp_before'], gb['ent_hpmax_before']) != (e_hp, e_max):
            note('hp', t, 'entity life before the tick %s model %s' % ((gb['ent_hp_before'], gb['ent_hpmax_before']), (e_hp, e_max)))
        gated = bool(flags & 0x48)
        exp = {}
        hurt_kind = None
        if gated:
            gated_ticks.add(t)
            exp.update(hp=e_hp, c1cc=c1cc, frame_counter_1C=ph['frame_counter_1C'], pos_x=ph['pos_x'], pos_y=ph['pos_y'], touching_slot=ph['touching_slot'])
            for k in ('anim_target', 'dir_target', 'anim_cur', 'force_x'):          # no animation pass, no physics pass: the animation and the knock-back stand still
                if h[k] != ph[k]:
                    note('gate', t, '%s %s changed from %s' % (k, h[k], ph[k]))
            exp_stats = (s_hp, s_max)
        else:
            tp = ph['touching_slot']
            c1cc_start = c1cc
            new_dir = None
            if flags & 0x34 and flags != 0x20:
                entry = None
            elif tp is not None:
                attr, power, tg = toucher_attack(res, t, tp)
                assert attr is not None and not (attr & 0x80), 'the model assumes an attack without the equipment bit'
                e_hp, _d = damage(inp, attr, power, e_hp)
                new_dir, margin = dir_model(ph['pos_x'] - tg['pos_x'], ph['pos_y'] - tg['pos_y'])
                entry = ('toucher', margin)
            elif c1cc_start == 0 and inp.boots < 3 and ((ph['vram_or_180'] & 0x180) >> 7) != 0:
                cost = HAZARD_COST[(ph['vram_or_180'] & 0x180) >> 7]
                e_hp = e_hp - cost if cost < e_hp else 0
                new_dir = (ph['dir_target'] + 16) & 31
                entry = ('hazard', 1.0)
            else:
                entry = None
            if entry is not None:
                hurt_kind = 'locked' if flags & 0x34 else 'normal'
                c1cc = 0x78
                if hurt_kind == 'normal':
                    exp['anim_target'] = 0x3A if ph['slope_18c'] == 4 else 0x31
                    if new_dir is not None:
                        exp['dir_target'] = new_dir
                        exp['_dir_margin'] = entry[1]
                else:
                    exp['anim_target'] = ph['anim_target']
                    exp['dir_target'] = ph['dir_target']
            elif e_hp == 0 and not (flags & 0x34):
                dead = True                                    # the death branch (E18): not modelled, only HP, +0x1CC, the stats and the regeneration stay checked
            if c1cc:
                c1cc -= 1                                      # counters pass, after the events pass
            # END of MovePlayer: regeneration, then the stats mirror
            if (flags & 0x7C) == 0 and hold == 0 and e_hp != 0 and e_hp < e_max:
                gain = 0
                for i in range(3):
                    p = inp.periods[i]
                    if p:
                        cnt[i] += 1
                        if p < cnt[i]:
                            cnt[i] = 0
                            gain += 1
                    else:
                        cnt[i] = 0
                e_hp = min(e_max, e_hp + gain)
            else:
                cnt = [0, 0, 0]
            s_max = clamp_max(e_max)
            s_hp = clamp_hp(e_hp, s_max)
            exp.update(hp=e_hp, c1cc=c1cc)
            exp_stats = (s_hp, s_max)
        if hurt_kind:
            hurts.append((t, hurt_kind, exp.get('anim_target')))
        # compare
        for k, v in exp.items():
            if k.startswith('_'):
                continue
            real = h[k] if k in h else None
            if k == 'dir_target':
                if real != v and not (exp.get('_dir_margin', 1.0) < 0.1 and ((real - v) & 31) in (1, 31)):
                    note('dir', t, 'dir %d model %d (margin %.2f)' % (real, v, exp.get('_dir_margin', 1.0)))
                continue
            if real != v:
                note({'hp': 'hp', 'c1cc': 'c1cc', 'anim_target': 'anim', 'frame_counter_1C': 'gate', 'pos_x': 'gate', 'pos_y': 'gate', 'touching_slot': 'gate'}.get(k, k), t, '%s %s model %s' % (k, real, v))
        if (row['g']['stats_hp'], row['g']['stats_hpmax']) != exp_stats:
            note('stats', t, 'stats %s model %s' % ((row['g']['stats_hp'], row['g']['stats_hpmax']), exp_stats))
        if h['hpmax_18'] != e_max:
            note('hpmax', t, 'hpmax %d model %d' % (h['hpmax_18'], e_max))
        if [row['g']['regen0'], row['g']['regen1'], row['g']['regen2']] != cnt:
            note('regen', t, 'counters %s model %s' % ([row['g']['regen0'], row['g']['regen1'], row['g']['regen2']], cnt))
        # lists pass: draw list membership from the row's own status and +0x1CC
        vis = (h['status'] in (2, 3)) and (h['c1cc'] & 3) != 3
        if bool(h['visible']) != vis:
            note('visible', t, 'visible %d model %d (c1cc %d)' % (h['visible'], vis, h['c1cc']))
        n_vis = sum(1 for e in [h] + row['targets'] if e['status'] in (2, 3) and (e['c1cc'] & 3) != 3)
        if h['visible_count'] != n_vis:
            note('visible', t, 'count %d model %d' % (h['visible_count'], n_vis))
        if (h['c1cc'] & 3 == 3) != bool(h['blink_expected']):
            note('visible', t, 'blink column')
    # ---- the hurt timeline of a ground hurt (normal path, nothing else in the next 15 ticks)
    timeline_ok, timeline_n = True, 0
    for i, (t, kind, anim) in enumerate(hurts):
        if kind != 'normal' or anim != 0x31 or dead or any(g in gated_ticks for g in range(t, t + 16)):
            continue
        nxt = hurts[i + 1][0] if i + 1 < len(hurts) else 10 ** 9
        if t + 15 >= nxt or t + 15 > len(rows):
            continue
        timeline_n += 1
        for k in range(0, 15):
            want = 0x31 if k <= 3 else 0x1C
            if rows[t - 1 + k]['hero']['anim_target'] != want:
                timeline_ok = False
                note('timeline', t + k, 'anim %X model %X' % (rows[t - 1 + k]['hero']['anim_target'], want))
        r15 = rows[t - 1 + 15]['hero']
        r14 = rows[t - 1 + 14]['hero']
        if r15['anim_target'] != 0 or r15['dir_target'] != ((r14['dir_target'] + 16) & 31):
            timeline_ok = False
            note('timeline', t + 15, 'idle flip: anim %X dir %d from %d' % (r15['anim_target'], r15['dir_target'], r14['dir_target']))
    # ---- the hit pass (scen.py's independent model of the 0.2.5 rule) when the hit pass runs on every tick
    if inp.every is None and not any(e[0] == 'flags' for evs in inp.events.values() for e in evs):
        mv = S.model_vs_real(res)
        bad_hits = [(k, mt, rt) for k, mt, rt, eq in mv if not eq]
        if bad_hits:
            note('hits', 0, str(bad_hits[:3]))
        rule('hit pass: ticks of every ordered pair equal scen.hit_model (%d pairs)' % len(mv), not bad_hits)
    names = [('boots', 'boots level equals the model of the inventory on every row'), ('sheet', 'hero sheet responses 1..11 equal kmodel.model_sheet on every row'),
             ('hp', 'hero HP per tick equals the model (damage rule, hazard cost and floor, regeneration)'), ('hpmax', 'hero HpMax per tick equals the model'),
             ('c1cc', 'hero +0x1CC per tick (0x77 on the hurt tick, then down by one, held by the tick gate)'),
             ('anim', 'hurt animation 0x31 / 0x3A on the hurt tick (unchanged by the locked 0x20 entry)'), ('dir', 'hurt direction (GetDirection of hero - toucher, or +0x8C + 0x10)'),
             ('visible', 'draw list: hero in it iff (+0x1CC & 3) != 3 (and the count of the list)'), ('stats', 'stats mirror: clamp(HpMax, 50), clamp(HP, HpMax), frozen by the tick gate'),
             ('regen', 'regeneration counters 0x80126FE8/EC/F0 per tick'), ('gate', 'tick gate [0x800DC4B8] & 0x48: +0x1C, position, TouchingEntity frozen')]
    for key, text in names:
        rule(text, key not in bad, '; '.join(bad.get(key, [])[:3]))
    if hurts and any(k == 'normal' for _t, k, a in hurts):
        rule('hurt timeline 0x31 x4, 0x1C x11, Idle with the direction flipped (%d hurts checked)' % timeline_n, timeline_ok and 'timeline' not in bad, '; '.join(bad.get('timeline', [])[:3]))
    rule('no unwritten-byte read during the run', not res['meta']['uninit_reads'], str(res['meta']['uninit_reads'][:3]))
    res['model_hurts'] = hurts
    res['model_dead'] = dead
    return out


def ok(results):
    return all(o for _n, o, _d in results)
