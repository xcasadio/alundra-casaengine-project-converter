"""run_e14.py - E14.a1 oracle: everything, in order.   usage: python -B docs/plan-e14-annexe/e14a/oracle/run_e14.py [--reuse-d1] [--only name,name]
exit code 0 = every verdict in out/summary.txt is PASS.   Needs Python 3 + capstone, the disc file D:/development/repo/Alundra Remake/Alundra (France)/Alundra (France)_extracted/ALUN_CD.EXE (+ DATA/BALANCE.BIN),
data-extracted/data/map_14.json, map_115.json, map_116.json, map_alundra.json, BALANCE.BIN.json (read only) and docs/plan-e19-d2c-valeurs.md (the D1 gate's reference, two levels up from tools/).

 1. D1 gate: tools/run_all.py (the copied base) -> out/d1/; S-A and S-B 216 values each, 0 differences, and every file equal to the one of E20.c2's out/d1 (read only reference, byte for byte).
 2. H3 equivalence: this folder's setup with every handler inert and the target at status 2 (the E20.c1 arena) must equal E20.c1's stored H3.json on the 45 ticks, every column of the hero and the target.
 3. the scenarios on emu.py -> out/<name>.txt / .json (rows, one line per handler call with the RNG word in and out), out/timeline.txt (the death timelines), out/arena.txt (what the live handlers change in the c1 arena).
 4. the same scenarios through the second interpreter (mips.py via interp2.py): rows, final entity memory -> out/diff2.txt.
 5. the independent model (e14model.py, written from the annexes) against every recorded handler call of the scenarios -> out/model-scenarios.txt.
 6. the single-call vectors (e14vec.py) against the model -> out/vectors.txt, out/vectors-<kind>.json.
 7. instruction coverage, read / write sets of the handlers -> out/coverage.txt, out/read-sets.txt.
 8. mutations (one-word patches of the emulated binary) -> out/mutations.txt.
 9. out/summary.txt (verdicts + key values), out/manifest.txt (md5 of every other file).
Byte-stable: no clock, no host path, no hash-ordered iteration in the files.  Elapsed times are printed, not written.
"""
import hashlib
import json
import os
import subprocess
import sys
import time

HERE = os.path.dirname(os.path.abspath(__file__))
TOOLS = os.path.join(HERE, 'tools')
sys.path.insert(0, HERE)
sys.path.insert(0, TOOLS)
OUT = os.path.join(HERE, 'out')
os.makedirs(OUT, exist_ok=True)
REPO = 'D:/development/repo/alundra-casaengine-project-converter'
C2_D1 = REPO + '/docs/plan-e20-annexe/e20-c2/oracle/out/d1'
C1_H3 = REPO + '/docs/plan-e20-annexe/e20-c1/oracle/out/H3.json'

import e14world as E
import e14run as R
import e14scen as S
import e14out as O
import e14check as K
import e14vec as V
import e14cov as VC
import c1world as C
import world as W
import hworld as H
import lib

T0 = time.time()


def log(msg):
    print('[%6.1fs] %s' % (time.time() - T0, msg))
    sys.stdout.flush()


def w(name, text):
    with open(os.path.join(OUT, name), 'w', encoding='utf-8', newline='\n') as f:
        f.write(text if text.endswith('\n') else text + '\n')


def md5(path):
    return hashlib.md5(open(path, 'rb').read()).hexdigest()


def norm(x):
    return json.loads(json.dumps(x, default=str, sort_keys=True))


KNOWN_TABLES = [(0x80022488, 0x80022688, 'atan 16x16 (GetDirectionToTarget)'), (0x80022688, 0x80022700, 'thresholds 15,31,... (GetDirectionToTarget)'), (0x80027014, 0x80027064, 'C1 jump table'),
                (0x800271DC, 0x80027214, 'C10 state jump table'), (0x80027214, 0x8002722C, 'C10 calm-step jump table'), (0x8002722C, 0x8002724C, 'C10 close jump table'),
                (0x8002724C, 0x80027264, 'C10 mid jump table'), (0x80027264, 0x80027284, 'C10 far jump table'), (0x80028B34, 0x80028B54, 'QUANT quarter-turn table'),
                (0x80028B54, 0x80028B58, 'cardinal table'), (0x80098708, 0x8009870C, 'RNG word'), (0x800C5F7C, 0x800C6300, 'item properties'), (0x80023644, 0x80023654, 'direction start table')]
VERDICTS = []


def verdict(text, ok):
    VERDICTS.append((text, bool(ok)))
    log('%s: %s' % ('PASS' if ok else 'FAIL', text))


# ---- 1. D1 gate ------------------------------------------------------------------------------------------------------------------------
def stage_d1():
    log('1. D1 gate')
    os.makedirs(os.path.join(OUT, 'd1'), exist_ok=True)
    if '--reuse-d1' in sys.argv:
        r = 0
    else:
        with open(os.path.join(OUT, 'd1', 'run_all.stdout.txt'), 'w', encoding='utf-8', newline='\n') as f:
            r = subprocess.run([sys.executable, '-B', os.path.join(TOOLS, 'run_all.py')], stdout=f, stderr=subprocess.STDOUT).returncode
    comp = open(os.path.join(OUT, 'd1', 'comparison.txt'), encoding='utf-8').read()
    n216 = comp.count('VERDICT PASS: 216 values compared, 0 differences')
    ok = (r == 0) and ('OVERALL: PASS' in comp) and n216 == 2
    verdict('D1 gate: S-A and S-B, 216 values each, 0 differences (tools/run_all.py of the copied base, exit code %d)' % r, ok)
    diffs, same = [], 0
    for fn in sorted(os.listdir(C2_D1)):
        if fn == 'run_all.stdout.txt':
            continue
        p1, p2 = os.path.join(OUT, 'd1', fn), os.path.join(C2_D1, fn)
        if not os.path.exists(p1) or md5(p1) != md5(p2):
            diffs.append(fn)
        else:
            same += 1
    verdict('D1 gate: the %d files of out/d1 equal E20.c2\'s out/d1 byte for byte (differences: %s)' % (same, diffs or 'none'), not diffs)


# ---- 2. H3 equivalence -------------------------------------------------------------------------------------------------------------------
def stage_h3eq():
    log('2. H3 equivalence (every handler inert, target at status 2)')
    st = json.load(open(C1_H3, encoding='utf-8'))
    spec = dict(name='H3eq', kind='beannoide', map=None, recs=[dict(x=20, y=11, h=0)], hero_px=(192, 96), n=45, inert=True, status2=True)
    m, meta = E.setup(spec)
    pad = W.Pad()

    def snap():
        return {'hero': norm(C.snap_entity(m, 0)), 'target': norm(C.snap_entity(m, 1)), 'rng': m.peek(C.RNG), 'latch': m.peeks(C.LATCH)}
    L = ['# H3 of E20.c1 (hero 60 px west of the inert Beannoid, Right held, 45 ticks) re-run with THIS folder\'s setup (EMachine, template init, map id word, spawn with force 1, hero life from the stats,',
         '# warm-up, drop stub) with every handler hooked inert and the target at status 2, against the stored e20-c1/oracle/out/H3.json: hero and target slot columns, RNG word, latch, per tick']
    a, b = snap(), st['pre']
    bad = 0
    ok0 = (a['hero'] == b['hero'] and a['target'] == b['targets'][0] and a['rng'] == b['rng'] and a['latch'] == b['latch_80146DF8'])
    L.append('tick 0: %s' % ('equal' if ok0 else 'DIFFERENT'))
    bad += 0 if ok0 else 1
    ncols = len(a['hero']) + len(a['target']) + 2
    for t in range(1, 46):
        m.tick_no = t
        H.htick(m, pad, W.RIGHT, 'emu', True)
        a, b = snap(), st['rows'][t - 1]
        ok = (a['hero'] == b['hero'] and a['target'] == b['targets'][0] and a['rng'] == b['rng'] and a['latch'] == b['latch_80146DF8'])
        if not ok:
            bad += 1
            L.append('tick %d: DIFFERENT' % t)
    L.append('%d ticks (0..45) x %d columns compared, ticks with a difference: %d' % (46, ncols, bad))
    w('h3-equivalence.txt', '\n'.join(L))
    verdict('H3 equivalence: the inert setup of this folder equals E20.c1\'s H3.json on 46 rows x %d columns (differences: %d)' % (ncols, bad), bad == 0)


# ---- 3. scenarios ------------------------------------------------------------------------------------------------------------------------
def only_filter():
    if '--only' in sys.argv:
        return set(sys.argv[sys.argv.index('--only') + 1].split(','))
    return None


def stage_scenarios():
    log('3. scenarios on emu.py')
    flt = only_filter()
    res = []
    for sp in S.scenarios():
        if flt and sp['name'] not in flt:
            continue
        t = time.time()
        r = R.run(sp, 'emu')
        O.write_text(r, OUT)
        O.write_json(r, OUT)
        res.append(r)
        log('   %-16s %4d ticks %5d handler calls  %.1fs' % (sp['name'], sp['n'], len(r['calls']), time.time() - t))
    return res


def rows_for_diff(r):
    return json.dumps([{k: v for k, v in row.items() if k not in ('calls',)} for row in [r['pre']] + r['rows']], sort_keys=True, default=str)


def stage_mips(res):
    log('4. scenarios through the second interpreter')
    L = ['# every scenario run again through mips.py (interp2.py) and compared with the emu.py run: rows (entities, hero, RNG word, latch, journal, probes), final entity memory (8 slots)']
    allok = True
    for r in res:
        sp = [s for s in S.scenarios() if s['name'] == r['spec']['name']][0]
        t = time.time()
        r2 = R.run(sp, 'mips')
        same_rows = rows_for_diff(r) == rows_for_diff(r2)
        same_mem = r['final_md5'] == r2['final_md5']
        ok = same_rows and same_mem
        allok = allok and ok
        L.append('%-16s rows %s, final memory %s (%d ticks)' % (sp['name'], 'equal' if same_rows else 'DIFFERENT', 'equal' if same_mem else 'DIFFERENT', sp['n']))
        log('   %-16s %s %.1fs' % (sp['name'], 'equal' if ok else 'DIFFERENT', time.time() - t))
    w('diff2.txt', '\n'.join(L))
    verdict('second interpreter: all %d scenarios give identical rows and final memory' % len(res), allok)


# ---- 5. model against the scenario calls ------------------------------------------------------------------------------------------------------
def e1_problems(c):
    """E1 = DestroyEntity(self, -1) 0x8003A59C: the invariants of the discovery annexes, checked on one call (scenario or vector)"""
    probs = []
    b, a = E.decode(c['ent_before']), E.decode(c['ent_after'])
    v = c.get('vec') or {}
    if a['status'] != 4:
        probs.append('status %d, not 4' % a['status'])
    if a['letter_228'] != -1:
        probs.append('+0x228 is %d, not -1' % a['letter_228'])
    if b['activeeffect_1B8'] and a['activeeffect_1B8'] != 0:
        probs.append('ActiveEffect +0x1B8 not cleared')
    fx = v.get('fx')
    fx = 8 if fx is None else fx                        # both sprites carry BreakEffect 8
    contents = b['contents_3C']
    jr = [(k, x) for k, x in c['journal']]
    kinds = [k for k, _ in jr]
    want = []
    if contents != 0 and not (contents in (0x26, 0x51, 0x52)):       # the MP veto: GetPlayerMpMax() == 0 in this world (stats +6)
        want += ['spawn_39F58', 'sound']
    if fx != 0:
        want += ['effect_3BE74']
    if kinds != want:
        probs.append('journal kinds %s, expected %s' % (kinds, want))
    for k, x in jr:
        if k == 'spawn_39F58' and (x[2] != contents + 0x1E or x[3] != b['posx'] & 0xFFFFFFFF):
            probs.append('spawn args %s' % x)
        if k == 'effect_3BE74' and (x[1] != fx or x[3] != c['a0']):
            probs.append('effect args %s' % x)
    return probs


def stage_model(res):
    log('5. independent model against the recorded handler calls of the scenarios')
    calls = [c for r in res for c in r['calls']]
    per, bad = K.check_calls([c for c in calls if c['handler'] != 'E1'])
    e1 = [c for c in calls if c['handler'] == 'E1']
    e1bad = [(c, e1_problems(c)) for c in e1 if e1_problems(c)]
    L = ['# the model of e14model.py (written from docs/plan-e14-annexe/e14a/muruta.md, beannoide.md, check.md and the disc tables) predicts, for every recorded call, the whole 0x294-byte entity image AFTER the call,',
         '# the RNG word after the call and the sounds; D1 / D9 also the HP (kmodel.damage_model, the E20.c1b model of ComputeNewHp).  E1 is checked against the annex invariants (status, +0x228, ActiveEffect, effect id, loot).']
    for h, (n, nb) in sorted(per.items()):
        L.append('%-4s %5d calls, %d differences' % (h, n, nb))
    L.append('%-4s %5d calls, %d differences' % ('E1', len(e1), len(e1bad)))
    for c, p in (bad + e1bad)[:20]:
        L.append('  %s t=%d slot %s: %s' % (c['handler'], c['tick'], c['slot'], p))
    w('model-scenarios.txt', '\n'.join(L))
    total = sum(n for n, _ in per.values()) + len(e1)
    verdict('independent model vs %d recorded scenario calls (A0 A1 C1 C10 D1 D9 E1): %d differences' % (total, len(bad) + len(e1bad)), not bad and not e1bad)
    return calls


# ---- 6. vectors ---------------------------------------------------------------------------------------------------------------------------
READ_E = ['anim_target88', 'dir_target8C', 'animdir98', 'anim_end_AC', 'forceadj_13C', 'onground_144', 'flags6C', 'hp', 'b0_274', 'b1_275', 'b3_277', 'ai0_280', 'ai1_282', 'posx', 'posy', 'posz',
          'tilex_120', 'tiley_124', 'floor_134', 'mh0_158', 'mh1_15C', 'mh2_160', 'mh3_164', 'touching_224', 'contents_3C', 'activeeffect_1B8', 'platform_28']


def vec_json(c):
    b, a, diff = O.call_fields(c)
    h = E.decode(c['hero_before'])
    v = {k: x for k, x in c['vec'].items() if k not in ('setup',)}
    return dict(handler=c['handler'], inputs=v, rng_in=c['rng_in'], rng_out=c['rng_out'], draws=O.draws_between(c['rng_in'], c['rng_out']), latch=c['latch'], v0=c['v0'],
                entity_before={k: b[k] for k in READ_E}, hero_before={k: h[k] for k in ('tilex_120', 'tiley_124', 'floor_134', 'posx', 'posy', 'box_1D4', 'curattack_1C8')}, hero_attack=c['hero_atk'],
                attacker_attack=c['attacker_atk'], changed=[[k, x, y] for k, x, y in diff], journal=c['journal'])


def stage_vectors():
    log('6. single-call vectors')
    L = ['# single-call vectors: the handler is called directly on a state the harness writes (RNG word = input and output); model = e14model.py; inputs are in the .json files', '']
    allbad = 0
    mips_diffs = 0
    cov = set()
    allcalls = []
    for kind, handlers in (('beannoide', ('C1', 'A1', 'D1', 'E1')), ('muruta', ('C10', 'A0', 'D9', 'E1'))):
        g = V.Gen(20261010 if kind == 'beannoide' else 20261011)
        m, sb = V.base(kind)
        vecs = []
        if kind == 'beannoide':
            vecs += V.c1_vectors(g) + V.a_vectors(g)[6:] + V.d_vectors(g, 'D1')
        else:
            vecs += V.c10_vectors(g) + V.a_vectors(g)[:6] + V.d_vectors(g, 'D9')
        vecs += V.e1_vectors(g, kind)
        t = time.time()
        recs = V.run_vectors(m, sb, vecs)
        cov |= set(m.cov)
        allcalls += recs
        m2, sb2 = V.base(kind)
        t2 = time.time()
        recs2 = V.run_vectors(m2, sb2, vecs, engine='mips')
        nd = sum(1 for a, b in zip(recs, recs2) if (a['ent_after'], a['rng_out'], a['journal']) != (b['ent_after'], b['rng_out'], b['journal']))
        L.append('== %s through the second interpreter (mips.py via interp2.py): %d vectors, differences in entity image / RNG word / journal: %d' % (kind, len(recs2), nd))
        mips_diffs += nd
        per, bad = K.check_calls([c for c in recs if c['handler'] != 'E1'])
        e1 = [c for c in recs if c['handler'] == 'E1']
        e1bad = [(c, e1_problems(c)) for c in e1 if e1_problems(c)]
        L.append('== %s (sprite kind %s): %d vectors' % (kind, kind, len(recs)))
        for h, (n, nb) in sorted(per.items()):
            L.append('  %-4s %5d vectors, %d model differences' % (h, n, nb))
        L.append('  %-4s %5d vectors, %d invariant violations' % ('E1', len(e1), len(e1bad)))
        for c, p in (bad + e1bad)[:20]:
            L.append('    %s %s: %s' % (c['handler'], {k: x for k, x in c['vec'].items() if k not in ('setup', 'rng')}, p))
        allbad += len(bad) + len(e1bad)
        # draws histogram per handler
        dr = {}
        for c in recs:
            n = O.draws_between(c['rng_in'], c['rng_out'])
            dr.setdefault(c['handler'], {}).setdefault(n, 0)
            dr[c['handler']][n] += 1
        L.append('  RNG draws per call (draws: vectors): ' + '; '.join('%s {%s}' % (h, ', '.join('%s: %d' % (k, x) for k, x in sorted(d.items(), key=lambda kv: -1 if kv[0] is None else kv[0]))) for h, d in sorted(dr.items())))
        L.append('')
        with open(os.path.join(OUT, 'vectors-%s.json' % kind), 'w', encoding='utf-8', newline='\n') as f:
            json.dump([vec_json(c) for c in recs], f, separators=(',', ':'), sort_keys=True, default=str)
            f.write('\n')
        log('   %s: %d vectors, model differences %d' % (kind, len(recs), len(bad) + len(e1bad)))
    w('vectors.txt', '\n'.join(L))
    verdict('single-call vectors (%d calls) vs the independent model / E1 invariants: %d differences' % (len(allcalls), allbad), allbad == 0)
    verdict('single-call vectors through the second interpreter: identical entity image, RNG word and journal on every vector (differences: %d)' % mips_diffs, mips_diffs == 0)
    return cov, allcalls



# ---- handler inputs = the previous row ------------------------------------------------------------------------------------------------------
HERO_READ = ('tilex_120', 'tiley_124', 'floor_134', 'posx', 'posy', 'box_1D4', 'curattack_1C8')


def stage_inputs(res):
    log('handler inputs against the previous row')
    ent_diff, hero_diff = {}, {}
    n = nh = nskip = 0
    for r in res:
        rows = [r['pre']] + r['rows']
        idx = {slot: i for i, (_rid, slot) in enumerate(r['meta']['slots'])}
        ev_ticks = set(r['spec']['before'].keys()) | set(t for t, _s, _d in r.get('ctrl_log', []))
        for c in r['calls']:
            t = c['tick']
            if t in ev_ticks:                                   # a harness write (state poke, hero teleport, sword policy) was made between the two ticks: not a clean comparison
                nskip += 1
                continue
            prev = rows[t - 1]['targets'][idx[c['slot']]]
            b = E.decode(c['ent_before'])
            n += 1
            for k in b:
                if b[k] != prev[k]:
                    ent_diff[k] = ent_diff.get(k, 0) + 1
            hb = E.decode(c['hero_before'])
            ph = rows[t - 1]['hero']
            nh += 1
            for k in HERO_READ:
                if hb[k] != ph[k]:
                    hero_diff[k] = hero_diff.get(k, 0) + 1
    L = ['# the state a handler sees at tick t, compared with the row of tick t-1 (= the state after the passes of the previous tick), over all scenario calls',
         'entity fields (all %d decoded fields), %d calls: fields that differ at least once: %s' % (len(E.FIELDS), n, ', '.join('%s x%d' % (k, v) for k, v in sorted(ent_diff.items())) or 'none'),
         'hero fields read by the handlers (%s), %d calls (same calls; the %d calls on a tick with a harness write between the two ticks are left out of both lines): differ: %s' % (', '.join(HERO_READ), nh, nskip,
                                                                                                                                   ', '.join('%s x%d' % (k, v) for k, v in sorted(hero_diff.items())) or 'none')]
    w('inputs-previous-row.txt', '\n'.join(L))
    ok = set(ent_diff) <= {'letter_228', 'status'}
    verdict('handler inputs: every entity field a handler sees at tick t equals the row of tick t-1, except %s (written by the events pass before the call)' % (sorted(ent_diff) or 'nothing'), ok)


# ---- 7. coverage, read sets ----------------------------------------------------------------------------------------------------------------------
def stage_cov(res, vcov):
    log('7. coverage and read / write sets')
    scov = set()
    for r in res:
        scov |= r['cov']
    L = ['# executed instructions of the live handlers and of the helpers they call (a function ends at the first `jr $ra` after its last forward branch target)', '', '== scenarios only ==']
    L += VC.text(scov)
    L += ['', '== single-call vectors only ==']
    L += VC.text(vcov)
    L += ['', '== scenarios + vectors ==']
    L += VC.text(scov | vcov)
    w('coverage.txt', '\n'.join(L))
    names = {}
    for nm, off, sg in E.FIELDS:
        names[off] = nm
    R_ = ['# memory read and written while a live handler call is open (the handler and everything it calls), union over the scenarios and the vectors.  Class: self = the enemy, hero = slot 0, tableN = the EXE image',
          '# (tables: QUANT 0x80028B34, atan 0x80022488, thresholds 0x80022688, jump tables, the balance blob...), global = anything else.  Offsets are entity-record offsets.']
    for h in sorted(E.READS):
        for kind, label in (('r', 'READ'), ('w', 'WRITTEN')):
            d = E.READS[h][kind]
            R_.append('')
            R_.append('== %s %s ==' % (h, label))
            groups = {}
            for (cls, off, n), cnt in d.items():
                groups.setdefault(cls, []).append((off, n, cnt))
            for cls in sorted(groups):
                items = sorted(groups[cls])
                if cls in ('self', 'hero', 'drop') or cls.startswith('slot'):
                    R_.append('  %s: %s' % (cls, ', '.join('+0x%X/%d%s' % (o, n, (' ' + names[o]) if (o in names and cls != 'drop') else '') for o, n, _c in items)))
                elif cls == 'table':
                    tabs = {}
                    for o, n, _c in items:
                        nm = 'other table'
                        for lo_, hi_, tn in KNOWN_TABLES:
                            if lo_ <= o < hi_:
                                nm = tn
                                break
                        tabs.setdefault(nm, [])
                        tabs[nm].append((o, o + n))
                    for tn in sorted(tabs):
                        ranges = []
                        for lo_, hi_ in sorted(tabs[tn]):
                            if ranges and lo_ <= ranges[-1][1] + 16:
                                ranges[-1][1] = max(ranges[-1][1], hi_)
                            else:
                                ranges.append([lo_, hi_])
                        R_.append('  table %s: %s' % (tn, ', '.join('0x%08X-0x%08X' % (x, y) for x, y in ranges[:12]) + (' ...' if len(ranges) > 12 else '')))
                else:
                    R_.append('  global: ' + ', '.join('0x%08X/%d' % (o, n) for o, n, _c in items))
    w('read-sets.txt', '\n'.join(R_))
    return scov


# ---- timelines ------------------------------------------------------------------------------------------------------------------------------
def stage_timeline(res):
    log('timelines')
    L = ['# death timelines: per kill, H = the tick whose hit pass wrote the lethal hit (+0x1D0 = 0x19 on the target), then the ticks read from the rows (all computed)',
         '# columns: record, slot, H (lethal hit), D (handler D call), anim 6 (death animation starts), E1 (DestroyEntity call = status 4), wipe (slot copied from the template: status 0, +0x48 = -1),',
         '#          last tick with 0x2C [record] still counting (count read BEFORE the tick), first tick with count 0 / Result 1', '']
    allok = True
    n = 0
    for r in res:
        slots = r['meta']['slots']
        rows = [r['pre']] + r['rows']
        probes = r['spec'].get('probes') or []
        for i, (rid, slot) in enumerate(slots):
            ecalls = [c['tick'] for c in r['calls'] if c['slot'] == slot and c['handler'] == 'E1']
            if not ecalls:
                continue
            hp = [x['targets'][i]['hp'] for x in rows]
            tk = [x['tick'] for x in rows]
            hits = [t for t, v, p in zip(tk[1:], [x['targets'][i]['c1d0'] for x in rows][1:], [x['targets'][i]['c1d0'] for x in rows][:-1]) if v == 0x19 and p < v]
            H_ = hits[-1]
            dtick = [c['tick'] for c in r['calls'] if c['slot'] == slot and c['handler'] in ('D1', 'D9') and c['tick'] > H_][0]
            a6 = [t for t, v, p in zip(tk[1:], [x['targets'][i]['anim_cur90'] for x in rows][1:], [x['targets'][i]['anim_cur90'] for x in rows][:-1]) if v == 6 and p != 6 and t > H_][0]
            e1 = ecalls[0]
            wipe = [t for t, x in zip(tk, rows) if x['targets'][i]['status'] == 0 and x['targets'][i]['ref48'] == -1 and t > e1][0]
            line = 'record %2d slot %d: H=%d D=H+%d anim6=H+%d E1=H+%d wipe=H+%d' % (rid, slot, H_, dtick - H_, a6 - H_, e1 - H_, wipe - H_)
            ok = (dtick - H_ == 1 and e1 - H_ == 23 and wipe - H_ == 24)
            if rid in probes:
                cnt = [(x['tick'], x['counts'][rid]) for x in r['rows']]
                last_counted = max(t for t, (m_, _res) in cnt if m_ > 0)
                first_zero = min(t for t, (m_, _res) in cnt if m_ == 0 and _res == 1)
                line += ' | 0x2C [%d]: last counted tick H+%d, first zero (Result 1) tick H+%d' % (rid, last_counted - H_, first_zero - H_)
                ok = ok and last_counted - H_ == 24 and first_zero - H_ == 25
            line += '   %s' % ('= the annex rule (E1 at H+23, still counted at H+24, zero at H+25)' if ok else 'DIFFERENT FROM THE ANNEX RULE')
            allok = allok and ok
            n += 1
            L.append('%-14s %s' % (r['spec']['name'], line))
    w('timeline.txt', '\n'.join(L))
    verdict('death timelines: %d kills, every one E1 at H+23, wipe at H+24, 0x2C counts it through H+24 and sees zero from H+25 (where probed)' % n, allok and n > 0)


# ---- arena impact ------------------------------------------------------------------------------------------------------------------------
def stage_arena(res):
    log('arena impact (live handlers in the E20.c1 arena)')
    L = ['# E20.c1 H1 / H3 with the live Beannoide (E14.a1) against the inert one of E20.c1 (stored H1.json / H3.json): what changes if the natives are not switched off in the arena', '']
    st1 = json.load(open(REPO + '/docs/plan-e20-annexe/e20-c1/oracle/out/H1.json', encoding='utf-8'))
    st3 = json.load(open(C1_H3, encoding='utf-8'))
    names = {r['spec']['name']: r for r in res}
    for nm, st, label in (('ARENA-H1-live', st1, 'H1 (hero 40 px west, Square at tick 1)'), ('ARENA-H3-live', st3, 'H3 (hero 60 px west, Right held)')):
        if nm not in names:
            continue
        r = names[nm]
        inert_first = [row['tick'] for row in st['rows'] if row['targets'][0]['c1d0'] == 0x19 or row['hero']['c1d0'] == 0x19][:1]
        live_rows = r['rows']
        live_t = [x['tick'] for x in live_rows if x['targets'][0]['c1d0'] == 0x19]
        live_h = [x['tick'] for x in live_rows if x['hero']['hp'] < 10]
        inert_h = [row['tick'] for row in st['rows'] if row['hero']['hp'] < 10]
        L.append('%s' % label)
        L.append('  inert (E20.c1): first tick with +0x1D0 = 0x19 on the target or on the hero: %s ; hero HP below 10 first at tick %s' % (inert_first, inert_h[:1] or 'never'))
        anim_tl = [(live_rows[0]['tick'], live_rows[0]['targets'][0]['anim_cur90'])] + [(live_rows[k]['tick'], live_rows[k]['targets'][0]['anim_cur90']) for k in range(1, len(live_rows))
                                                                                         if live_rows[k]['targets'][0]['anim_cur90'] != live_rows[k - 1]['targets'][0]['anim_cur90']]
        L.append('  live (this oracle): hit written on the target at ticks %s ; hero HP below 10 first at tick %s ; target animation timeline %s' % (
            live_t[:6], live_h[:1] or 'never', ' '.join('%d:%d' % x for x in anim_tl[:12])))
        L.append('')
    w('arena.txt', '\n'.join(L))


# ---- 8. mutations -------------------------------------------------------------------------------------------------------------------------
def mutation_list():
    def word(a):
        return lib.u32(a)
    muts = []
    muts.append(('D9 hit animation 2 -> 3 (0x8007DE24 addiu v0,zero,2)', 0x8007DE24, word(0x8007DE24), word(0x8007DE24) + 1))
    muts.append(('C1 case 0 reach: rel0 < 3 -> < 4 (0x80065F44 slti)', 0x80065F44, word(0x80065F44), word(0x80065F44) + 1))
    muts.append(('C1 case 0 floor limit 0x200000 -> 0x210000 (0x80065F60 lui)', 0x80065F60, word(0x80065F60), word(0x80065F60) + 1))
    muts.append(('C10 state 5 death flag 0x40 -> 0x20 (0x800688C4 ori)', 0x800688C4, word(0x800688C4), word(0x800688C4) - 0x20))
    muts.append(('HandleAnimationDirection slt -> sltu (0x80080170)', 0x80080170, word(0x80080170), word(0x80080170) + 1))
    muts.append(('StartWalk chase draw rng(0x65) -> rng(0x64) (0x8007FF18 addiu)', 0x8007FF18, word(0x8007FF18), word(0x8007FF18) - 1))
    muts.append(('DestroyEntity status 4 -> 5 (0x8003A5B8 addiu)', 0x8003A5B8, word(0x8003A5B8), word(0x8003A5B8) + 1))
    muts.append(('QUANT table byte 9: 8 -> 0 (word 0x80028B3C)', 0x80028B3C, word(0x80028B3C), (word(0x80028B3C) & ~0x0000FF00) | 0))
    # the C10 tail mask: find `andi $v0, $v0, 0x1c` after 0x80068904
    for i in lib.dis(0x80068904, 6):
        if i.mnemonic == 'andi' and i.op_str.replace(' ', '').endswith('0x1c'):
            muts.append(('C10 tail mask 0x1C -> 0x1F (0x%08X andi)' % i.address, i.address, word(i.address), (word(i.address) & ~0xFFFF) | 0x1F))
            break
    muts.append(('CONTROL: C10 map-0x99 branch (dead for sprite 347), load offset 0xC -> 0x10 (0x80068178)', 0x80068178, word(0x80068178), word(0x80068178) + 4))
    return muts


def stage_mutations():
    log('8. mutations')
    L = ['# one-word patches of the emulated binary (RAM only, applied after the setup).  Each is run against (a) the single-call vectors of the handler family it touches, checked by the model, and (b) one scenario',
         '# whose trace is compared with the unpatched trace.  CAUGHT = the model disagrees on some vector OR the scenario trace changes.  The CONTROL patches a branch no scenario or vector reaches: nothing may change.', '']
    base_trace = {}
    allok = True

    def run_family(kind, vecs_fn, scen_name):
        m, sb = V.base(kind)
        recs = V.run_vectors(m, sb, vecs_fn(V.Gen(99)))
        per, bad = K.check_calls([c for c in recs if c['handler'] != 'E1'])
        e1bad = [c for c in recs if c['handler'] == 'E1' and e1_problems(c)]
        r = R.run(S.by_name(scen_name))
        return len(bad) + len(e1bad), json.dumps([[row['rng'], [t['anim_cur90'] for t in row['targets']], [t['hp'] for t in row['targets']], [t['posx'] for t in row['targets']], [t['status'] for t in row['targets']]]
                                                  for row in r['rows']]), len(recs)

    fam = [('beannoide', lambda g: V.c1_vectors(g) + V.d_vectors(g, 'D1')[:120] + V.e1_vectors(g, 'beannoide'), 'BN-death'),
           ('muruta', lambda g: V.c10_vectors(g) + V.d_vectors(g, 'D9')[:120] + V.e1_vectors(g, 'muruta'), 'MU-death')]
    base = {}
    E.PATCHES[:] = []
    for kind, fn, sc in fam:
        base[kind] = run_family(kind, fn, sc)
        L.append('baseline %-9s: %d vectors, %d model differences ; scenario %s trace recorded' % (kind, base[kind][2], base[kind][0], sc))
    L.append('')
    for desc, addr, old, new in mutation_list():
        E.PATCHES[:] = [(addr, old, new)]
        out = {}
        for kind, fn, sc in fam:
            out[kind] = run_family(kind, fn, sc)
        E.PATCHES[:] = []
        caught_v = {k: out[k][0] - base[k][0] for k in out}
        changed = {k: out[k][1] != base[k][1] for k in out}
        caught = any(v > 0 for v in caught_v.values()) or any(changed.values())
        control = desc.startswith('CONTROL')
        ok = (not caught) if control else caught
        allok = allok and ok
        L.append('%-86s  vector differences: beannoide %d, muruta %d ; trace changed: BN-death %s, MU-death %s  -> %s' % (
            desc, caught_v['beannoide'], caught_v['muruta'], changed['beannoide'], changed['muruta'], ('as expected (nothing changes)' if control else 'CAUGHT') if ok else ('FAILED: a control changed' if control else 'NOT CAUGHT')))
    w('mutations.txt', '\n'.join(L))
    verdict('mutations: every one-word patch of a handler or helper is caught, the control changes nothing', allok)


# ---- 9. summary, manifest -------------------------------------------------------------------------------------------------------------------
def stage_summary(res):
    L = ['# E14.a1 oracle: verdicts', '']
    for t, ok in VERDICTS:
        L.append('%s  %s' % ('PASS' if ok else 'FAIL', t))
    L += ['', '# scenarios: key facts (computed)']
    for r in res:
        L.append('')
        L.append('## %s: %s' % (r['spec']['name'], r['spec']['desc']))
        L += ['  ' + x for x in O.fact_lines(r)]
    w('summary.txt', '\n'.join(L))
    ok = all(v for _t, v in VERDICTS)
    return ok


def stage_manifest():
    lines = []
    for root, _dirs, files in os.walk(OUT):
        for fn in sorted(files):
            p = os.path.join(root, fn)
            rel = os.path.relpath(p, OUT).replace('\\', '/')
            if rel in ('manifest.txt', 'd1/run_all.stdout.txt'):
                continue
            lines.append('%s  %s' % (md5(p), rel))
    lines.sort(key=lambda s: s.split('  ', 1)[1])
    w('manifest.txt', '\n'.join(lines))
    log('manifest: %d files' % len(lines))


def main():
    flt = only_filter()
    if not flt:
        stage_d1()
        stage_h3eq()
    res = stage_scenarios()
    stage_timeline(res)
    stage_arena(res)
    if not flt:
        stage_mips(res)
    stage_model(res)
    stage_inputs(res)
    cov, _calls = stage_vectors()
    stage_cov(res, cov)
    if not flt:
        stage_mutations()
    ok = stage_summary(res)
    stage_manifest()
    log('ALL PASS' if ok else 'SOME VERDICT FAILED')
    return 0 if ok else 1


if __name__ == '__main__':
    sys.exit(main())
