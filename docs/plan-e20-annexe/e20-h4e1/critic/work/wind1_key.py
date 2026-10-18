"""wind1_key.py - h4e1 discovery (read-only): the REMADE per-tick key of the wind-1 cast (item 0x31, driver 0x80035EB0) on a COPY of the e20-h4 harness (h4o/, which is the h4d1 critic's
copy of the e20-h4 oracle: spellworld.py + world.py + machine.py + the real MIPS code of ALUN_CD.EXE).

What this script adds to the harness (nothing else is changed in h4o/):
  * FAITHFUL slot init (R-EL-1): before tick 1 the 64 slots get +0 = slot index and +0x48 = -1 (map-load init 0x8003B24C-0x8003B2D8, 0x8003B2BC), and the template 0x80134368 gets +0x48 = -1.
    FAITHFUL=0 in the environment gives the stale (h4) keys: used only to prove the difference.
  * PF (R-EL-2 / plan section 1.20 point 3): the per-frame generator step of 0x8002BD0C-0x8002BD34 (s <- s * 0x7D2B89DD + 0xE06A02E7) AFTER the recording of the tick's own state (rng1) and before the
    next tick (rng0 of t+1 = step(rng1 of t)).
  * watchers (no code is replaced): the query call of the driver (0x80035FAC = just after the jal 0x8003AF70 returns), the choice (0x80036028), the random branch (0x80036074), the two spawns of the
    driver (return points 0x8003605C and 0x8003613C), the no-effect destroy (0x80036158), the gate (0x80035F40).
  * variants (only for the 'what if' keys; never for the faithful ones): GATE_OFF (nop the animation gate at 0x80035F40: the first strike is no longer lost), QUERY_FIX (the corrected query of
    h4b1 D-H4B1-2: inclusive bound = the count word raised by one during the query call, sort from 2 results = the immediate of 0x8003B0B4 patched from 3 to 2).
  * scenario poses: hero position / direction, static ClassA targets (global sprite 25, real SpawnWarpEntity, ClassA bit given as trace_targets.py does), a decoy (a sprite-25 block WITHOUT ClassA, so
    never a candidate) born last so that the highest occupied slot is never a candidate (the exclusive bound of the binary query then never hides a candidate), a Z for a target, a kill + a reborn
    block in the same slot (slot reuse), the hurt poke of R-EL-6 (hero animation 0x31 right before tick T), a hold of the Round button.

usage (from a COPY of this folder, python -B): python -B wind1_key.py OUTDIR [--only NAME ...]
writes OUTDIR/<scenario>.json (rows + queries + spawns), OUTDIR/<scenario>.txt (a short summary) and OUTDIR/MANIFEST.sha1 (sha-1 of every file, sorted by name)."""
import os
import sys
import json
import hashlib
import argparse

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, 'h4o'))
import spellworld as S
import world as W
import trace_targets as T
import machine as MM
from machine import M32

HERO = 0x80127D30
STRIDE = 0x294
RNG = 0x80098708
MULT, ADD = 0x7D2B89DD, 0xE06A02E7
BLOCK = 0x800E4338
FAITHFUL = os.environ.get('FAITHFUL', '1') == '1'
COUNTW = 0x80134360
PTRS, KEYS = 0x800CC058, 0x800CC158
LASTT = 0x8012712C
PHASE, CNT, INUSE = 0x80127160, 0x8012715C, 0x80127164
TABLE = 0x800235E0


def ent(i):
    return HERO + STRIDE * i


def slot_of(p):
    if p == 0:
        return 0
    d = (p & M32) - HERO
    if d % STRIDE == 0 and 0 <= d // STRIDE < 64:
        return d // STRIDE
    return -1


# ---- watchers: a class-level wrapper of Machine.exec1 (the interpreter itself is untouched) -----------------------------------------------------------------------------------------------
_orig_exec1 = MM.Machine.exec1


def _exec1(self, pc, w):
    wt = getattr(self, 'watch', None)
    if wt:
        cb = wt.get(pc)
        if cb is not None:
            cb(self)
    return _orig_exec1(self, pc, w)


MM.Machine.exec1 = _exec1


def lcg(s):
    return (s * MULT + ADD) & M32


def draws_between(a, b):
    s = a
    for n in range(0, 80):
        if s == b:
            return n
        s = lcg(s)
    return -1


def snap_ent(m, i):
    e = ent(i)
    g = m.peeks
    return dict(status=g(e + 0x10), id=g(e + 4), type=g(e + 0x68), flags=m.peek(e + 0x6C), hp=g(e + 0x14), x=g(e + 0x114), y=g(e + 0x118), z=g(e + 0x11C),
                fx=g(e + 0xC4), fy=g(e + 0xC8), fz=g(e + 0xB8), cc=g(e + 0xCC), d0=g(e + 0xD0), anim_t=g(e + 0x88), anim_c=g(e + 0x90), dir_t=g(e + 0x8C),
                animend=g(e + 0xAC), b4=g(e + 0xB4), ground=g(e + 0x144), adj13c=g(e + 0x13C), c20=g(e + 0x20), cnt1c=g(e + 0x1C),
                c1cc=g(e + 0x1CC), c1d0=g(e + 0x1D0), hitc=g(e + 0x220), touch=slot_of(m.peek(e + 0x224)), curatk=m.peek(e + 0x1C8) & M32,
                box=[g(e + 0x1D4 + 4 * k) for k in range(6)])


def run(name, ticks=140, seed=0x12345678, hero_px=(100, 100), hero_dir=24, targets=(), decoy=None, pf=True, hurt=None, gate_off=False, query_fix=False,
        kill=None, circle_hold=None, item=0x31, mp=4, cells=None):
    """targets: list of dicts {x, y, z(raw, optional), solid_class_a(bool, default True)} in slot order; decoy: (x, y) of a non-ClassA block born after the targets; kill: (tick, target_index, new_xy) =
    before that tick the target's status is set to 4 (destroyed) and, right after the next slot pass, a new ClassA block is born at new_xy (slot reuse)."""
    m, STATS, placed = S.make(item=item, mp=mp, mpmax=4, seed=seed, indices=(1, 2, 16, 17, 19, 20, 21, 22, 23, 24, 25))
    P = ent(0)
    if cells:                                            # raised cells (height byte * 16 px) poked into the tile table after the flat field was built (world.py build_world layout)
        for row in range(60):
            for col in range(52):
                hb, sl, wk, gp = cells(col, row)
                a = W.TILES + 0x604 + row * W.ROW + col * 8
                m.poke16(a, wk | (gp << 8))
                m.poke8(a + 2, sl)
                m.poke8(a + 3, hb)
    if FAITHFUL:
        m.poke(0x80134368 + 0x48, 0xFFFFFFFF)
        for i in range(1, 64):
            m.poke(ent(i), i)
            m.poke(ent(i) + 0x48, 0xFFFFFFFF)
    m.poke(P + 0x8C, hero_dir)
    if tuple(hero_px) != (100, 100):
        m.poke(P + 0x114, hero_px[0] << 16)
        m.poke(P + 0x118, hero_px[1] << 16)
    tptrs = []
    for t in targets:
        p = T.spawn_global(m, 25, t['x'], t['y'], t.get('z', 0))
        assert p, 'target spawn failed'
        if t.get('class_a', True):
            m.poke(p + 0x6C, m.peek(p + 0x6C) | 1)
        m.poke(p + 0x14, t.get('hp', 5000))
        if 'zraw' in t:
            m.poke(p + 0x11C, t['zraw'] & M32)
        if 'xraw' in t:
            m.poke(p + 0x114, t['xraw'] & M32)
        tptrs.append(p)
    decoy_ptr = None
    if decoy:
        decoy_ptr = T.spawn_global(m, 25, decoy[0], decoy[1], 0)
        assert decoy_ptr
    # code patches (the variants)
    if gate_off:
        assert m.peek(0x80035F40) == 0x146200A9
        m.poke(0x80035F40, 0)                               # the animation gate 'bne v1, v0 (0x33), exit' becomes a nop
    if query_fix is True:                                    # query_fix == 'bound' raises the count word only (inclusive bound), the sort stays the binary one (from 3 results)
        assert m.peek(0x8003B0B4) == 0x2A020003
        m.poke(0x8003B0B4, 0x2A020002)                      # the sort starts from 2 results
    m.journal.clear()
    pad = W.Pad()
    # ---- watchers -----------------------------------------------------------------------------------------------------------------
    cur = dict(queries=[], spawns=[], events=[])
    state = dict(t=0, pending_q=None, raised=False)

    def w_query_entry(mm):
        pass

    def w_query_ret(mm):
        n = mm.r[2] & M32
        if query_fix and state.get('raised'):
            mm.poke(COUNTW, mm.peek(COUNTW) - 1)
            state['raised'] = False
        cands = []
        for k in range(n):
            cands.append((slot_of(mm.peek(PTRS + 4 * k)), mm.peek(KEYS + 4 * k)))
        cur['queries'].append(dict(t=state['t'], counter=mm.peeks(CNT), count_word=mm.peeks(COUNTW), n=n, cands=cands, chosen=None, branch=None))

    def w_query_call(mm):                                   # at the first instruction of the query function: query_fix raises the count word by one for the call
        if query_fix:
            mm.poke(COUNTW, mm.peek(COUNTW) + 1)
            state['raised'] = True

    def w_choice(mm):
        q = cur['queries'][-1]
        q['chosen'] = slot_of(mm.r[16])
        q['branch'] = 'target' if mm.r[16] else 'random'

    def w_spawn_entry(mm):
        if not state.get('in_tick'):
            return
        sp = mm.r[29]
        cur['spawns'].append(dict(t=state['t'], args=[mm.r[4] & M32, mm.r[5] & M32, mm.r[6] & M32, mm.r[7] & M32] + [mm.peek(sp + 0x10 + 4 * k) for k in range(3)], ret=None, caller=slot_of(mm.r[4])))

    def w_spawn_ret(mm):
        cur['spawns'][-1]['ret'] = slot_of(mm.r[2])

    def w_nofx(mm):
        cur['events'].append(dict(t=state['t'], ev='destroy_nofx_8003A774', slot=slot_of(mm.r[4])))

    m.watch = {0x80035FAC: w_query_ret, 0x80036028: w_choice, 0x80039F58: w_spawn_entry, 0x8003605C: w_spawn_ret, 0x8003613C: w_spawn_ret, 0x80036158: w_nofx}
    if query_fix:
        m.watch[0x8003AF70] = w_query_call
    rows = []
    jn = 0
    killed = False
    prev_1d0 = {p: 0 for p in tptrs}
    for t in range(1, ticks + 1):
        state['t'] = t
        # head of the tick: resolve the touch of the previous hit pass exactly as fire2_targets_c.py does (ComputeNewHp = the D handler of the enemies); no invulnerability
        resolved = []
        for k, p in enumerate(tptrs):
            tc = m.peek(p + 0x224)
            if tc:
                before = m.peeks(p + 0x14)
                m.call(0x8003A374, p)
                resolved.append((k, m.peeks(tc + 0x68), before - m.peeks(p + 0x14)))
        if kill and t == kill[0]:
            p = tptrs[kill[1]]
            m.poke(p + 0x10, 4)                              # destroyed: the slot pass of this very tick recycles it (template copy, status 0)
        hero_hold = W.CIRCLE if t == 1 else 0
        if circle_hold and circle_hold[0] <= t <= circle_hold[1]:
            hero_hold |= W.CIRCLE
        if hurt is not None and t == hurt:
            m.poke(P + 0x88, 0x31)
        cur['queries'], cur['spawns'], cur['events'] = [], [], []
        rng0 = m.peek(RNG)
        m.tick_no = t
        state['in_tick'] = True
        W.tick(m, pad, hero_hold, check_pad=False)
        state['in_tick'] = False
        rng1 = m.peek(RNG)
        rng2 = lcg(rng1) if pf else rng1
        if pf:
            m.poke(RNG, rng2)
        if kill and t == kill[0]:
            # the reborn block: born right after the tick that recycled the slot, i.e. at the head of the next tick (before its slot pass is irrelevant: the slot was freed by this tick's pass)
            np_ = T.spawn_global(m, 25, kill[2][0], kill[2][1], 0)
            assert np_
            m.poke(np_ + 0x6C, m.peek(np_ + 0x6C) | 1)
            m.poke(np_ + 0x14, 5000)
            tptrs[kill[1]] = np_
            prev_1d0[np_] = 0
            killed = True
        ents = {}
        for i in range(1, 64):
            if m.peeks(ent(i) + 0x10):
                ents[i] = snap_ent(m, i)
        jr = [(j[1], list(j[2]) if isinstance(j[2], (list, tuple)) else j[2]) for j in m.journal[jn:] if j[0] == t]
        jn = len(m.journal)
        snd = [a for (k, a) in jr if k == 'sound' and a != 0]
        fx = [(k, a) for (k, a) in jr if k.startswith('effect')]
        hits = []
        for k, p in enumerate(tptrs):
            c = m.peeks(p + 0x1D0)
            if c == 0x19 and prev_1d0.get(p, 0) != 0x19:
                hits.append(k)
            prev_1d0[p] = c
        rows.append(dict(t=t, rng0=rng0, rng1=rng1, rng2=rng2, draws=draws_between(rng0, rng1), hero=snap_ent(m, 0),
                         item=dict(inuse=m.peeks(INUSE), phase=m.peeks(PHASE), counter=m.peeks(CNT), last_target_slot=slot_of(m.peek(LASTT)), last_target_raw=m.peek(LASTT)),
                         mp=m.peek(STATS + 4) & 0xFFFF, count_word=m.peeks(COUNTW), ents=ents, queries=list(cur['queries']), spawns=list(cur['spawns']), events=list(cur['events']),
                         sounds=snd, effects=fx, hit_targets=hits, resolved=resolved))
    meta = dict(name=name, ticks=ticks, seed=seed, hero_px=list(hero_px), hero_dir=hero_dir, faithful=FAITHFUL, pf=pf, hurt=hurt, gate_off=gate_off, query_fix=query_fix,
                targets=[dict(t) for t in targets], decoy=decoy, kill=kill, circle_hold=circle_hold, item=item, target_slots=[slot_of(p) for p in tptrs], decoy_slot=slot_of(decoy_ptr) if decoy_ptr else None,
                table=[m.rh(TABLE + 2 * k) for k in range(9)])
    return dict(meta=meta, rows=rows)


def summary(res):
    out = []
    meta = res['meta']
    out.append('scenario %s : %s' % (meta['name'], json.dumps({k: v for k, v in meta.items() if k not in ('name', 'table')}, sort_keys=True)))
    last = None
    for r in res['rows']:
        h = r['hero']
        key = (h['anim_t'], r['item']['inuse'], r['item']['phase'])
        if key != last:
            out.append('  t=%3d hero anim_t=%#x item_in_use=%#x phase=%d counter=%d mp=%d' % (r['t'], h['anim_t'], r['item']['inuse'], r['item']['phase'], r['item']['counter'], r['mp']))
            last = key
    for r in res['rows']:
        for q in r['queries']:
            out.append('  t=%3d QUERY counter(before incr)=%d count_word=%d n=%d cands=%s -> %s %s' % (r['t'], q['counter'], q['count_word'], q['n'], q['cands'], q['branch'], q['chosen']))
        for s in r['spawns']:
            out.append('  t=%3d SPAWN sprite=%d x=%d y=%d z=%d dir=%d (raw args %s) -> slot %s' % (r['t'], s['args'][2], s['args'][3] if s['args'][3] < 2 ** 31 else s['args'][3] - 2 ** 32,
                                                                                          s['args'][4], s['args'][5], s['args'][6], 'ok', s['ret']))
        for e in r['events']:
            out.append('  t=%3d EVENT %s' % (r['t'], e))
        if r['draws'] > 0 or r['hit_targets']:
            out.append('  t=%3d draws=%d rng %#010x -> %#010x -> %#010x hit_targets=%s' % (r['t'], r['draws'], r['rng0'], r['rng1'], r['rng2'], r['hit_targets']))
    births = {}
    for r in res['rows']:
        for i, e in r['ents'].items():
            if e['type'] == 22:
                b = births.setdefault((i, e['id']), dict(first=r['t'], last=r['t'], x=e['x'], y=e['y'], z0=e['z']))
                b['last'] = r['t']
    for (i, eid), b in sorted(births.items(), key=lambda kv: kv[1]['first']):
        out.append('  strike slot %d id %d : ticks %d..%d (life %d) at px (%.4f, %.4f) z0=%d' % (i, eid, b['first'], b['last'], b['last'] - b['first'] + 1, b['x'] / 65536, b['y'] / 65536, b['z0']))
    last_row = res['rows'][-1]
    out.append('  final: hero anim_t %#x item_in_use %#x phase %d counter %d mp %d' % (last_row['hero']['anim_t'], last_row['item']['inuse'], last_row['item']['phase'], last_row['item']['counter'], last_row['mp']))
    freed = next((r['t'] for r in res['rows'] if r['item']['inuse'] == 0 and r['t'] > 1), None)
    out.append('  object freed at tick %s' % freed)
    return '\n'.join(out) + '\n'


def scen_list():
    S_ = []
    DEC = (500, 300)                                     # a non-ClassA block, born after the targets, far from everything: the highest occupied slot is never a candidate
    # R-EL-4 bench: flat field, hero (100,100) facing east (24), seed 0x12345678 at tick 1, no map entities, Circle at tick 1
    S_.append(('wind1_cast_pf', dict(ticks=140)))
    S_.append(('wind1_cast_pf_gateoff', dict(ticks=140, gate_off=True)))                 # Q-H4-2 what-if: 8 strikes
    S_.append(('wind1_cast_pf_south', dict(ticks=140, hero_dir=0)))                       # the direction does not matter: same positions expected
    # the table of the 64 slots full (63 non-ClassA blocks): every birth is refused (SpawnWarpEntity returns 0), the draws of the random strikes are made all the same
    S_.append(('wind1_full_table_pf', dict(ticks=140, targets=[dict(x=500 + (i % 10) * 10, y=300 + (i // 10) * 10, class_a=False) for i in range(63)])))
    # interruptions R-EL-6
    for T_ in (20, 31, 33, 41, 42, 60, 102, 103):
        S_.append(('wind1_hurt%d_pf' % T_, dict(ticks=T_ + 60, hurt=T_)))
    # edges of the map (strikes born outside the field)
    S_.append(('wind1_edge_west_x20_pf', dict(ticks=140, hero_px=(20, 100))))
    S_.append(('wind1_edge_north_y10_pf', dict(ticks=140, hero_px=(100, 10))))
    # a raised block of cells (height byte 4 = 64 px) at columns 1..2, rows 3..8 (x 24..71, y 48..143): three of the seven random strikes land on it
    S_.append(('wind1_plateau_h64_pf', dict(ticks=140, cells=lambda col, row: (4, 0, 0, 0) if (1 <= col <= 2 and 3 <= row <= 8) else (0, 0, 0, 0))))
    # one near target (140,100), distance 40: bin = binary query (no decoy), dec = decoy (bound irrelevant), fix = corrected query (inclusive bound, sort from 2) without decoy
    one = [dict(x=140, y=100)]
    S_.append(('wind1_one_near_bin_pf', dict(ticks=150, targets=one)))
    S_.append(('wind1_one_near_dec_pf', dict(ticks=150, targets=one, decoy=DEC)))
    S_.append(('wind1_one_near_fix_pf', dict(ticks=150, targets=one, query_fix=True)))
    S_.append(('wind1_one_near_dec_gateoff_pf', dict(ticks=150, targets=one, decoy=DEC, gate_off=True)))
    # the six static targets of e20-h4 (targets_inv0): ties at distance 40 (four of them) exercise the exact sort
    six = [dict(x=x, y=y) for (x, y) in ((140, 100), (100, 140), (180, 100), (100, 60), (60, 100), (140, 140))]
    S_.append(('wind1_six_dec_pf', dict(ticks=150, targets=six, decoy=DEC)))
    S_.append(('wind1_six_bin_pf', dict(ticks=150, targets=six)))
    S_.append(('wind1_six_fix_pf', dict(ticks=150, targets=six, query_fix=True)))
    S_.append(('wind1_six_dec_gateoff_pf', dict(ticks=150, targets=six, decoy=DEC, gate_off=True)))
    # the production defaults if the author keeps the plan's defaults (Q-H4B-2/3 corrected query AND Q-H4E1-1 first strike kept): both what-ifs together, no decoy
    S_.append(('wind1_one_near_fixgate_pf', dict(ticks=150, targets=one, query_fix=True, gate_off=True)))
    S_.append(('wind1_six_fixgate_pf', dict(ticks=150, targets=six, query_fix=True, gate_off=True)))
    # D8: the first non-skipped candidate decides, a far one stops the search (fewer than 3 results: no sort, slot order)
    far, near, far2 = dict(x=350, y=100), dict(x=140, y=100), dict(x=100, y=400)
    S_.append(('wind1_d8_far_first_dec_pf', dict(ticks=110, targets=[far, near], decoy=DEC)))
    S_.append(('wind1_d8_near_first_dec_pf', dict(ticks=110, targets=[near, far], decoy=DEC)))
    S_.append(('wind1_d8_three_sorted_dec_pf', dict(ticks=110, targets=[far, near, far2], decoy=DEC)))
    S_.append(('wind1_d8_far_first_fix_pf', dict(ticks=110, targets=[far, near], query_fix=True)))        # corrected: the sort from 2 results puts the near one first
    S_.append(('wind1_d8_far_first_fixgate_pf', dict(ticks=110, targets=[far, near], query_fix=True, gate_off=True)))
    # the 160 px edge (distance squared 0x6400 passes, 0x6401 stops), one target each, decoy
    for nm, tg, hero in (('east160', dict(x=260, y=100), (100, 100)), ('east161', dict(x=261, y=100), (100, 100)),
                         ('south160', dict(x=100, y=260), (100, 100)), ('south161', dict(x=100, y=261), (100, 100)),
                         ('east160_frac', dict(x=260, y=100, xraw=(260 << 16) + 0xFFFF), (100, 100)), ('east161_frac', dict(x=260, y=100, xraw=(261 << 16)), (100, 100)),
                         ('west160_h200', dict(x=40, y=100), (200, 100)), ('west161_h200', dict(x=39, y=100), (200, 100))):
        S_.append(('wind1_edge_%s_dec_pf' % nm, dict(ticks=90, targets=[tg], decoy=DEC, hero_px=hero)))
    # the Z gate: target at hero Z + 80 px exactly (passes) and one raw unit higher (skipped); the hero Z is read from the key (1 raw)
    S_.append(('wind1_zgate_pass_dec_pf', dict(ticks=90, targets=[dict(x=140, y=100, zraw=1 + 0x500000)], decoy=DEC)))
    S_.append(('wind1_zgate_skip_dec_pf', dict(ticks=90, targets=[dict(x=140, y=100, zraw=1 + 0x500001)], decoy=DEC)))
    # the stale last target: the target of the first strike dies and another block is born in the same slot
    # A=(140,100) slot 1 is struck first; before tick 45 it is destroyed and another block A' is born in the same slot at (150,130) (out of the strike's box); the binary compares POINTERS, so A' is skipped as
    # 'the last target' and the strike goes to B=(150,150); the control has no kill: A is still excluded by its hit counter and by the last target, B is chosen too, but the query lists differ
    ab = [dict(x=140, y=100), dict(x=150, y=150)]
    S_.append(('wind1_slot_reuse_dec_pf', dict(ticks=110, targets=ab, decoy=DEC, kill=(45, 0, (150, 130)))))
    S_.append(('wind1_slot_control_dec_pf', dict(ticks=110, targets=ab, decoy=DEC)))
    return S_


if __name__ == '__main__':
    ap = argparse.ArgumentParser()
    ap.add_argument('outdir')
    ap.add_argument('--only', nargs='*')
    a = ap.parse_args()
    os.makedirs(a.outdir, exist_ok=True)
    for name, kw in scen_list():
        if a.only and name not in a.only:
            continue
        res = run(name, **kw)
        json.dump(res, open(os.path.join(a.outdir, name + '.json'), 'w'), sort_keys=True)
        open(os.path.join(a.outdir, name + '.txt'), 'w').write(summary(res))
        print(name, 'ok')
