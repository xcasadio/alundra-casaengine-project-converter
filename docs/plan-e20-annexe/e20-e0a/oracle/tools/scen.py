"""scen.py - E20.c1: the scenarios H1-H5 (+ controls), the readers of a run, the independent model of the hit rule, and the text/JSON writers.

The scenario specs hold SETUP (positions, targets, the target's animation in H4b) and INPUTS (the pad word per tick).  They hold no expected output.
Everything printed is read from the emulated machine.  The model (hit_model) is written from docs/plan-e20-heros.md 0.2.5 and is fed only with the geometry/counter columns
of a run; it is a cross-check of the real hit pass, not an input of the oracle.
"""
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import c1world as C
import world as W

SQ, RIGHT = W.SQUARE, W.RIGHT
TXPOS, TYPOS = 20, 11                      # map entity record fields of the first target: X = 12 * 20 + 12 = 252 px, Y = 8 * 11 + 8 = 96 px
TX, TY = C.target_px(TXPOS, TYPOS)
SWING_ANIM = 0x10                          # basic sword, ground swing (E20.b oracle AT1)


def hero_at(dx, dy=0):
    """hero start so that target - hero = (dx, -dy) px: hero x = TX - dx, hero y = TY + dy (dy = hero y - target y)"""
    return (TX - dx, TY + dy)


def spec(name, desc, dx, dy=0, n=40, holds=None, after=0, targets=None, target_init=None, experiments=True, mutate=None, seed=0x12345678, **extra):
    """extra (only the scenarios H6-H8 use it, so the specs and the JSON of H1-H5 are unchanged): kind (a real sprite record of another map as the target, c1world.KINDS),
       direction (hero facing, default 24 = east), multi (several attackers may contest a victim: the model of the whole tick is used), labels ({target index: text})"""
    d = dict(name=name, desc=desc, hero_px=hero_at(dx, dy), targets=targets if targets is not None else [(TXPOS, TYPOS, 0)], n=n, holds=holds or {}, after=after,
             target_init=target_init, experiments=experiments, mutate=mutate, seed=seed, dx=dx, dy=dy)
    d.update(extra)
    return d


def or_flags(bits):
    """setup of a target: OR `bits` into its class/flags word (+0x6C, as InitializeEntity left it).  H7 gives an inert Beannoid the HitsA class bit (bit 2) this way"""
    def f(m, slot):
        a = C.ent(slot) + 0x6C
        m.poke(a, m.peek(a) | bits)
    return f


def bean_anim(anim):
    def f(m, slot):
        m.poke(C.ent(slot) + 0x88, anim)           # TargetAnimationId: stands for what the target's own (not run) program would write
    return f


def scenarios(reach):
    """reach = (max dx with a contact at dy = 0) measured by the sweep; H2 is one pixel further"""
    return [
        spec('H1', 'hero at rest faces the Beannoid 40 px east (same Y), Square at tick 1 (basic sword, ground swing 0x10), 40 ticks', 40, holds={1: SQ}),
        spec('H1-edge', 'control: as H1 at the measured reach limit (%d px)' % reach, reach, holds={1: SQ}),
        spec('H2', 'as H1 one pixel beyond the measured reach limit (%d px): no contact expected' % (reach + 1), reach + 1, holds={1: SQ}),
        spec('H3', 'contact damage: the hero stands 60 px west of the inert Beannoid and walks east (Right held from tick 1), 45 ticks', 60, after=RIGHT, n=45),
        spec('H4a', 'attack inside the target\'s +0x1D0 window: Square at tick 1 and at tick 23 (the tick after the first swing ends), dx 40, 60 ticks', 40, n=60, holds={1: SQ, 23: SQ}),
        spec('H4b', 'target in Beannoid animation 6 (animation-set byte 0xD = 0x40, "AnimFlags 0x40"), same swing as H1', 40, target_init={0: bean_anim(6)}, holds={1: SQ}),
        spec('H5', 'two Beannoids in reach of one swing: targets at +40,0 and +52,+8 px from the hero, Square at tick 1', 40,
             targets=[(TXPOS, TYPOS, 0), (TXPOS + 1, TYPOS + 1, 0)], holds={1: SQ}),
        spec('H6', 'immune target: as H1 (dx 40, Square at tick 1) but the target is a REAL sprite whose balance response for the sword\'s nibble (attack attribute 0x81, nibble 1) is 0x80: '
             'sprite 126 of map 69, balance record 382 (DamageResponses[0] = 128)', 40, holds={1: SQ}, kind='map69-sprite126', multi=True),
        spec('H7', 'first attacker wins: the hero (slot 0, swing box from tick 1, dx 30) and a second attacker, an inert Beannoid in slot 2 (12 px east of the target, its own frame box and '
             'its own attack attr 0x01 from its real animation, its flags +0x6C got the HitsA bit 2 from the harness), both reach the target (slot 1) on tick 1', 30,
             targets=[(TXPOS, TYPOS, 0), (TXPOS + 1, TYPOS, 0)], target_init={1: or_flags(4)}, holds={1: SQ}, multi=True,
             labels={1: 'second attacker: its +0x6C has bit 2 (HitsA) set by the harness'}),
        spec('H8', 'another direction: as H1 facing west (hero direction 8), the Beannoid 40 px WEST of the hero (dx -40), Square at tick 1', -40, holds={1: SQ}, direction=8),
    ]


# ---- readers -----------------------------------------------------------------------------------------------------------------
def kb(e):
    """FrameCollision pointer present"""
    return 1 if e['frame_box_1D4'] else 0


def nsize(v):
    """stored size (n << 16) - 1 -> n px (stored -1 or 0: 0)"""
    return (v + 1) >> 16


def hit_ticks(res, victim, attacker_slot):
    """ticks at which entity `victim` (key 'hero' or target index) has +0x1D0 == 0x19 (a hit written by the pass this tick) - with its TouchingEntity slot"""
    out = []
    for r in res['rows']:
        e = r['hero'] if victim == 'hero' else r['targets'][victim]
        if e['c1d0'] == 0x19:
            out.append((r['tick'], e['touching_slot']))
    return out


def sparks(r):
    """effect id 9 calls of a row: list of (cx, cy, cz) raw 16.16"""
    return [(a[3], a[4], a[5]) for k, a in r['journal_raw'] if k == 'effect_3BDD8' and a[1] == 9]


def effects_on_targets(r):
    return [(a[1], a[3]) for k, a in r['journal_raw'] if k == 'effect_3BE74']


def prog_letters(r):
    return [a[1] for k, a in r['journal_raw'] if k == 'handler']


# ---- the independent model of the hit rule ---------------------------------------------------------------------------------
def axis_ok(a, asize, t, tsize):
    d = a - t
    if d >= 0:
        return d < tsize + 1
    return -d < asize + 1


def class_mask(flags):
    m = ((flags >> 2) & 1) | ((flags << 2) & 8)
    if flags & 0x1000:
        m |= 0x800
    return m


def hit_model(res, attacker, victim):
    """ticks at which `attacker` touches `victim` according to the rule of 0.2.5, from the rows only.  attacker / victim: 'hero' or an int target index.
       State carried by the model itself: the victim's +0x1D0 counter (decremented by the counters pass before the hit pass of each tick, set to 0x19 by a hit)."""
    def ent(r, k):
        return r['hero'] if k == 'hero' else r['targets'][k]
    c1d0 = ent(res['pre'], victim)['c1d0']
    out = []
    for r in res['rows']:
        A, T = ent(r, attacker), ent(r, victim)
        if c1d0:
            c1d0 -= 1                                                    # counters pass 0x80038998
        ok = (A['frame_box_1D4'] != 0 and A['cur_attack_1C8'] != 0 and (A['atk_attr'] or 0) != 0)
        ok = ok and T['status'] in (2, 3) and T['ctl_20'] == 0
        ok = ok and class_mask(A['flags_6C']) != 0 and (T['flags_6C'] & class_mask(A['flags_6C'])) != 0
        ok = ok and c1d0 == 0 and T['c1cc'] == 0 and (T['anim_flags_B4'] & 0x40) == 0
        if ok:
            ok = (axis_ok(A['hitbox_x'], A['bsize_x'], T['modded_x'], T['tsize_x']) and axis_ok(A['hitbox_y'], A['bsize_y'], T['modded_y'], T['tsize_y'])
                  and axis_ok(A['hitbox_z'], A['bsize_z'], T['modded_z'], T['tsize_z']))
        if ok:
            out.append(r['tick'])
            c1d0 = 0x19
    return out


_BAL = None


def balance_response(index, attr):
    """DamageResponses[(attr & 0xF) - 1] of balance record `index`, from the decoded balance file (data, not from the machine); the sheet bytes [nibble + 2] of the record start
       at the first response, so nibble n reads DamageResponses[n - 1]"""
    global _BAL
    if _BAL is None:
        _BAL = json.load(open(os.path.join(C.DATA, 'BALANCE.BIN.json'), encoding='utf-8-sig'))['EntriesBySpriteIndex']
    n = attr & 0xF
    return _BAL[index]['DamageResponses'][n - 1] if 1 <= n <= 11 else None


def target_balance_index(res, i):
    """balance index of target i of a run: the sprite table index of its kind | 0x100 (the entity byte 5 has bit 0x80 in both kinds used)"""
    kind = res['spec'].get('kind')
    return C.kind_data(kind).balance_index if kind else C.SPRITE_TABLE_INDEX | 0x100


def hit_model_multi(res):
    """the rule of 0.2.5 applied to the whole tick, for scenarios where several attackers may contest a victim: per tick, +0x1D0 of every entity is decremented (counters pass), then the attackers
       are taken in list order (hero, then the targets in slot order), each against every other entity in list order; a valid pair writes +0x1D0 = 0x19 on the victim (so a later attacker
       is blocked on that victim) and sets TouchingEntity unless the victim's response byte for the attacker's attribute nibble has (r & 0xC0) == 0x80 (immune: the code then
       skips the TouchingEntity store).  Fed only with the geometry/counter columns of the rows and the decoded balance file.
       Returns {(attacker, victim): {'ticks': [...], 'touching': [...]}} where 'touching' lists the ticks at which the victim's TouchingEntity is set by this pair."""
    n = len(res['meta']['target_slots'])
    keys = ['hero'] + list(range(n))

    def ent(r, k):
        return r['hero'] if k == 'hero' else r['targets'][k]

    def bal(k):
        return 0 if k == 'hero' else target_balance_index(res, k)
    c1d0 = {k: ent(res['pre'], k)['c1d0'] for k in keys}
    out = {(a, v): dict(ticks=[], touching=[]) for a in keys for v in keys if a != v}
    for r in res['rows']:
        for k in keys:
            if c1d0[k]:
                c1d0[k] -= 1                                             # counters pass 0x80038998
        for a in keys:
            for v in keys:
                if a == v:
                    continue
                A, T = ent(r, a), ent(r, v)
                ok = (A['frame_box_1D4'] != 0 and A['cur_attack_1C8'] != 0 and (A['atk_attr'] or 0) != 0)
                ok = ok and T['status'] in (2, 3) and T['ctl_20'] == 0
                ok = ok and class_mask(A['flags_6C']) != 0 and (T['flags_6C'] & class_mask(A['flags_6C'])) != 0
                ok = ok and c1d0[v] == 0 and T['c1cc'] == 0 and (T['anim_flags_B4'] & 0x40) == 0
                if ok:
                    ok = (axis_ok(A['hitbox_x'], A['bsize_x'], T['modded_x'], T['tsize_x']) and axis_ok(A['hitbox_y'], A['bsize_y'], T['modded_y'], T['tsize_y'])
                          and axis_ok(A['hitbox_z'], A['bsize_z'], T['modded_z'], T['tsize_z']))
                if ok:
                    out[(a, v)]['ticks'].append(r['tick'])
                    c1d0[v] = 0x19
                    resp = balance_response(bal(v), A['atk_attr'])
                    if not (resp is not None and (resp & 0xC0) == 0x80):
                        out[(a, v)]['touching'].append(r['tick'])
    return out


def geometry_ticks(res, attacker, victim):
    """ticks at which the attacker's box overlaps the victim's body on the three axes, with the attacker's box, attack and class mask valid, IGNORING the victim's +0x1D0, +0x1CC and AnimFlags 0x40"""
    def ent(r, k):
        return r['hero'] if k == 'hero' else r['targets'][k]
    out = []
    for r in res['rows']:
        A, T = ent(r, attacker), ent(r, victim)
        ok = (A['frame_box_1D4'] != 0 and A['cur_attack_1C8'] != 0 and (A['atk_attr'] or 0) != 0 and T['status'] in (2, 3) and T['ctl_20'] == 0
              and class_mask(A['flags_6C']) != 0 and (T['flags_6C'] & class_mask(A['flags_6C'])) != 0)
        if ok:
            ok = (axis_ok(A['hitbox_x'], A['bsize_x'], T['modded_x'], T['tsize_x']) and axis_ok(A['hitbox_y'], A['bsize_y'], T['modded_y'], T['tsize_y'])
                  and axis_ok(A['hitbox_z'], A['bsize_z'], T['modded_z'], T['tsize_z']))
        if ok:
            out.append(r['tick'])
    return out


def real_hits(res, attacker_slot, victim):
    """ticks at which the real pass wrote a hit from `attacker_slot` on `victim`: victim +0x1D0 == 0x19 and (if not immune) TouchingEntity == attacker"""
    out = []
    for r in res['rows']:
        e = r['hero'] if victim == 'hero' else r['targets'][victim]
        if e['c1d0'] == 0x19 and e['touching_slot'] == attacker_slot:
            out.append(r['tick'])
    return out


def model_vs_real(res):
    """for every ordered pair (attacker, victim) of the scenario: ticks by the model and by the real pass; returns list of (pair, model, real, equal)"""
    n = len(res['meta']['target_slots'])
    keys = ['hero'] + list(range(n))
    slot = {'hero': 0}
    for i, s in enumerate(res['meta']['target_slots']):
        slot[i] = s
    out = []
    if res['spec'].get('multi'):
        mm = hit_model_multi(res)
        for a in keys:
            for v in keys:
                if a == v:
                    continue
                m = mm[(a, v)]
                touch = real_hits(res, slot[a], v)                                 # hit written with TouchingEntity == attacker
                written = [r['tick'] for r in res['rows']                          # hit written on an immune victim: +0x1D0 = 0x19, no TouchingEntity, the attacker's HitCounter set
                           if (r['hero'] if v == 'hero' else r['targets'][v])['c1d0'] == 0x19 and (r['hero'] if v == 'hero' else r['targets'][v])['touching_slot'] is None
                           and (r['hero'] if a == 'hero' else r['targets'][a])['hit_counter'] > 0]
                out.append(((a, v), m['ticks'], sorted(touch + written), m['ticks'] == sorted(touch + written)))
                out.append(((a, v, 'TouchingEntity'), m['touching'], touch, m['touching'] == touch))
        return out
    for a in keys:
        for v in keys:
            if a == v:
                continue
            mt = hit_model(res, a, v)
            rt = real_hits(res, slot[a], v)
            out.append(((a, v), mt, rt, mt == rt))
    return out


# ---- writers ---------------------------------------------------------------------------------------------------------------
def _col(name, f):
    return (name, f)


ENT_COLS = [
    ('tick', lambda r, e: r['tick']), ('hold', lambda r, e: '0x%04X' % r['hold']),
    ('status', lambda r, e: e['status']), ('anim_tgt88', lambda r, e: '0x%X' % e['anim_target']), ('anim_cur90', lambda r, e: '0x%X' % e['anim_cur']),
    ('dir_tgt8C', lambda r, e: e['dir_target']), ('dir_cur94', lambda r, e: e['dir_cur']), ('animflags_B4', lambda r, e: '0x%X' % e['anim_flags_B4']),
    ('force_x', lambda r, e: e['force_x']), ('force_y', lambda r, e: e['force_y']), ('force_z', lambda r, e: e['force_z']),
    ('pos_x', lambda r, e: C.px(e['pos_x'])), ('pos_y', lambda r, e: C.px(e['pos_y'])), ('pos_z_raw', lambda r, e: e['pos_z']), ('pos_z_dll', lambda r, e: e['pos_z_dll']),
    ('on_ground', lambda r, e: e['on_ground']),
    ('box_1D4', lambda r, e: kb(e)), ('boff_x', lambda r, e: C.px(e['boff_x']) if kb(e) else '-'), ('boff_y', lambda r, e: C.px(e['boff_y']) if kb(e) else '-'),
    ('boff_z', lambda r, e: C.px(e['boff_z']) if kb(e) else '-'),
    ('bsize_x', lambda r, e: e['bsize_x'] if kb(e) else '-'), ('bsize_y', lambda r, e: e['bsize_y'] if kb(e) else '-'), ('bsize_z', lambda r, e: e['bsize_z'] if kb(e) else '-'),
    ('atk_ptr_1C8', lambda r, e: '0x%08X' % e['cur_attack_1C8']), ('atk_attr', lambda r, e: '-' if e['atk_attr'] is None else '0x%02X' % e['atk_attr']),
    ('atk_pow', lambda r, e: '-' if e['atk_power'] is None else e['atk_power']),
    ('hitbox_x', lambda r, e: C.px(e['hitbox_x'])), ('hitbox_y', lambda r, e: C.px(e['hitbox_y'])), ('hitbox_z', lambda r, e: C.px(e['hitbox_z'])),
    ('modded_x', lambda r, e: C.px(e['modded_x'])), ('modded_y', lambda r, e: C.px(e['modded_y'])), ('modded_z', lambda r, e: C.px(e['modded_z'])),
    ('tsize_x', lambda r, e: e['tsize_x']), ('tsize_y', lambda r, e: e['tsize_y']), ('tsize_z', lambda r, e: e['tsize_z']),
    ('touching_224', lambda r, e: '-' if e['touching_slot'] is None else 'slot%d' % e['touching_slot']), ('hit_counter_220', lambda r, e: e['hit_counter']),
    ('c1CC', lambda r, e: e['c1cc']), ('c1D0', lambda r, e: e['c1d0']), ('cnt_1C', lambda r, e: e['frame_counter_1C']), ('hp_14', lambda r, e: e['hp']),
]


def ent_table(rows, getter, extra=None):
    cols = ENT_COLS + (extra or [])
    out = ['\t'.join(c[0] for c in cols)]
    for r in rows:
        e = getter(r)
        out.append('\t'.join(str(c[1](r, e)) for c in cols))
    return out


def write_scenario(res, outdir):
    sp = res['spec']
    name = sp['name']
    rows = [res['pre']] + res['rows']
    L = []
    L.append('# %s: %s' % (name, sp['desc']))
    L.append('# computed by the emulated ALUN_CD.EXE (France): real tick passes, real hit pass; effects/sound/spawn and the target\'s own programs are journalled stubs (see c1world.py)')
    L.append('# hero start px %s, direction 24 (east), targets (xpos,ypos,height) %s -> px %s, pad: %s, after-last %s, ticks %d, RNG seed 0x%08X'
             % (sp['hero_px'], sp['targets'], [list(t['px']) for t in res['meta']['targets']], {k: '0x%X' % v for k, v in sorted(sp['holds'].items())}, '0x%X' % sp['after'], sp['n'], sp['seed']))
    L.append('# per-tick values are the state AFTER the passes of that tick; tick 0 = start state.  positions in px (exact decimals), PosZ raw = binary convention (rest = terrain + 1), pos_z_dll = raw - 1;')
    L.append('# boff/bsize = the converted frame box (+0x208.. / +0x214..; sizes stored (n<<16)-1, raw); hitbox = +0x1FC..; modded = +0x1D8.. (ModdedPos); tsize = +0x1F0.. (target body sizes, stored (n<<16)-1)')
    L.append('')
    L.append('## HERO (slot 0)')
    L += ent_table(rows, lambda r: r['hero'])
    for i, s in enumerate(res['meta']['target_slots']):
        L.append('')
        if res['meta'].get('target_kind'):
            L.append('## TARGET %d (%s, slot %d)' % (i, res['meta']['target_kind'], s))
        else:
            L.append('## TARGET %d (Beannoid, slot %d, balance record index 335)%s' % (i, s, (' - ' + sp['labels'][i]) if i in sp.get('labels', {}) else ''))
        L += ent_table(rows, lambda r, i=i: r['targets'][i], extra=[('prog_chosen', lambda r, e, s=s: ''.join(C.LETTERS[a[1]] if 0 <= a[1] < 6 else '?' for k, a in r.get('journal_raw', []) if k == 'handler' and a[0] == s) or '-')])
    L.append('')
    L.append('## per tick: RNG word 0x80098708, latched hero animation 0x80146DF8, journal of stub calls (effects, sounds, handler stubs)')
    L.append('tick\trng\tlatch_80146DF8\tjournal')
    for r in rows:
        L.append('%d\t0x%08X\t%d\t%s' % (r['tick'], r['rng'], r['latch_80146DF8'], ' | '.join(r.get('journal', [])) or '-'))
    L.append('')
    L.append('## ComputeNewHp experiments (called from the harness inside the target\'s D handler stub, on copies of the machine; the machine is restored after each)')
    if not res['experiments']:
        L.append('(none: no D program was chosen for a target in this run)')
    for ex in res['experiments']:
        c = ex['context']
        L.append('tick %d, target slot %d: hp %d, TouchingEntity slot %s, attacker CurrentAttack ptr 0x%08X attr %s power %s, latched hero animation %d, target response byte [(attr&0xF)+2] = %s'
                 % (ex['tick'], ex['slot'], c['target_hp_before'], (c['touching'] - C.ENT0) // C.STRIDE, c['attack_ptr'],
                    '-' if c['attack_attr'] is None else '0x%02X' % c['attack_attr'], c['attack_power'], c['latched_anim'], c['response']))
        for v in ('live', 'empty', 'filled'):
            x = ex['variants'][v]
            eqs = '; '.join('[%d] %s' % (q['k'], 'null' if not q['ptr'] else 'blob+0x%X level %d count %d attacks %s' % (q['ptr_off'], q['level'], q['attack_count'], q['attacks'])) for q in x['equipment'])
            L.append('   %-6s returns %d (HP == 0), hp %d -> %d (damage %d); equipment records: %s%s' % (
                v, x['returns'], x['hp_before'], x['hp_after'], x['damage'], eqs, ('; UNSET READS %s' % x['unset_reads']) if x['unset_reads'] else ''))
    L.append('')
    L += describe(res)
    open(os.path.join(outdir, name + '.txt'), 'w', encoding='utf-8', newline='\n').write('\n'.join(L) + '\n')
    js = dict(spec={k: v for k, v in sp.items() if k not in ('target_init', 'mutate')}, meta=res['meta'], pre=res['pre'], rows=res['rows'], experiments=res['experiments'])
    open(os.path.join(outdir, name + '.json'), 'w', encoding='utf-8', newline='\n').write(json.dumps(js, indent=1, sort_keys=True, default=str) + '\n')


def describe(res):
    """key facts of a run, computed from the rows"""
    L = ['## KEY FACTS (computed from the rows above)']
    rows = res['rows']
    slots = res['meta']['target_slots']
    n = len(slots)
    # contacts as victim
    for v in ['hero'] + list(range(n)):
        ev = []
        for r in rows:
            e = r['hero'] if v == 'hero' else r['targets'][v]
            if e['c1d0'] == 0x19:
                ev.append(r['tick'])
        name = 'hero' if v == 'hero' else 'target %d (slot %d)' % (v, slots[v])
        L.append('hit written on %s (+0x1D0 = 0x19) at ticks: %s' % (name, ev or 'never'))
    for r in rows:
        vic = []
        for v in ['hero'] + list(range(n)):
            e = r['hero'] if v == 'hero' else r['targets'][v]
            if e['c1d0'] == 0x19:
                vic.append('%s (+0x1D0 = 0x19, TouchingEntity %s)' % ('hero' if v == 'hero' else 'target %d (slot %d)' % (v, slots[v]),
                                                                       ('slot %d' % e['touching_slot']) if e['touching_slot'] is not None else 'NONE (immune)'))
        if vic:
            sp = sparks(r)
            L.append('   tick %d: hit written on: %s; HitCounter after the pass: hero %d%s; %d sparks (effect 9) at %s; effects 4/5 on targets %s; RNG word after the tick 0x%08X'
                     % (r['tick'], '; '.join(vic), r['hero']['hit_counter'], ''.join(', target %d %d' % (i, t['hit_counter']) for i, t in enumerate(r['targets'])), len(sp),
                        [(C.px(a_), C.px(b_), C.px(c_)) for a_, b_, c_ in sp], effects_on_targets(r) or 'none', r['rng']))
    # counters-pass clear
    for i, r in enumerate(rows[:-1]):
        nxt = rows[i + 1]
        for v in ['hero'] + list(range(n)):
            e = r['hero'] if v == 'hero' else r['targets'][v]
            f = nxt['hero'] if v == 'hero' else nxt['targets'][v]
            if e['hit_counter'] and not f['hit_counter']:
                L.append('   tick %d -> %d: %s HitCounter %d cleared by the counters pass' % (r['tick'], nxt['tick'], 'hero' if v == 'hero' else 'target %d' % v, e['hit_counter']))
            if e['touching'] and not f['touching']:
                L.append('   tick %d -> %d: %s TouchingEntity cleared by the counters pass' % (r['tick'], nxt['tick'], 'hero' if v == 'hero' else 'target %d' % v))
    # program letters of the targets
    for i, s in enumerate(slots):
        seq = []
        for r in rows:
            for k, a in r['journal_raw']:
                if k == 'handler' and a[0] == s:
                    seq.append((r['tick'], C.LETTERS[a[1]] if 0 <= a[1] < 6 else '?'))
        non_c = [(t, l) for t, l in seq if l != 'C']
        L.append('program chosen for target %d (slot %d) by the real events pass: C on %d ticks; other letters %s' % (i, s, sum(1 for t, l in seq if l == 'C'), non_c or 'none'))
    # changes of the hero's animation / HP / hurt counter, and of the targets' animation (computed from the rows)
    def chg(key, who):
        out = []
        prev = (res['pre']['hero'] if who == 'hero' else res['pre']['targets'][who])[key]
        for r in rows:
            e = r['hero'] if who == 'hero' else r['targets'][who]
            if e[key] != prev:
                out.append((r['tick'], prev, e[key]))
            prev = e[key]
        return out
    L.append('hero anim_target (+0x88) changes (tick, from, to): %s' % (', '.join('t%d 0x%X->0x%X' % c for c in chg('anim_target', 'hero')) or 'none'))
    L.append('hero HP (+0x14) changes: %s' % (', '.join('t%d %d->%d' % c for c in chg('hp', 'hero')) or 'none'))
    L.append('hero +0x1CC jumps up (tick, from, to; the counters pass of the same tick has already decremented): %s'
             % (', '.join('t%d %d->%d' % c for c in chg('c1cc', 'hero') if c[2] > c[1]) or 'none'))
    L.append('hero direction target (+0x8C) changes: %s' % (', '.join('t%d %d->%d' % c for c in chg('dir_target', 'hero')) or 'none'))
    for i in range(n):
        L.append('target %d anim_cur (+0x90) changes: %s; HP changes: %s; position changes: %s' % (
            i, ', '.join('t%d 0x%X->0x%X' % c for c in chg('anim_cur', i)) or 'none', ', '.join('t%d %d->%d' % c for c in chg('hp', i)) or 'none',
            ', '.join('t%d x %s->%s' % (c[0], C.px(c[1]), C.px(c[2])) for c in chg('pos_x', i)) or 'none'))
    L.append('hero sheet 0x80127008 bytes [3..13] (the responses for attack nibbles 1..11, byte [nibble + 2]) at the end of the run: %s' % res['meta'].get('end_hero_sheet_0x80127008_responses'))
    # geometry overlap ignoring the gates (+0x1D0, +0x1CC, AnimFlags 0x40): which overlapping ticks were NOT hits
    for a in ['hero'] + list(range(n)):
        for v in ['hero'] + list(range(n)):
            if a == v:
                continue
            geo = geometry_ticks(res, a, v)
            if geo:
                real = real_hits(res, 0 if a == 'hero' else slots[a], v)
                L.append('boxes of %s overlap %s (class mask ok, box + attack present) at ticks %s; real hits at %s; overlapping ticks that were not hits: %s'
                         % ('hero' if a == 'hero' else 'target %d' % a, 'hero' if v == 'hero' else 'target %d' % v, geo, real, [t for t in geo if t not in real] or 'none'))
    # model vs real
    for key, mt, rt, eq in model_vs_real(res):
        if len(key) == 2:
            L.append('model vs real hit ticks, attacker %s on victim %s: model %s, real %s -> %s' % (key[0], key[1], mt, rt, 'EQUAL' if eq else 'DIFFERENT'))
        else:
            L.append('model vs real ticks at which attacker %s set TouchingEntity of victim %s: model %s, real %s -> %s' % (key[0], key[1], mt, rt, 'EQUAL' if eq else 'DIFFERENT'))
    m = res['machine']
    L.append('unwritten-byte reads during the run: %s' % ('none' if not m.uninit_reads else sorted((hex(pc), hex(a), nn, c) for (pc, a, nn), c in m.uninit_reads.items())))
    return L
