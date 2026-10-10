"""e14out.py - E14.a1 oracle: readers (computed facts), the text and JSON writers.  Everything printed is read from the emulated machine; nothing here knows an expected value."""
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.join(HERE, 'tools'))
import e14world as E
import e14run as R
import c1world as C
from machine import M32, s32

ENT0, STRIDE = E.ENT0, E.STRIDE
LETTERS = 'ABCDEF'


def px(v):
    return R.px(v)


def draws_between(a, b, limit=24):
    """number of RNG steps n (0..limit) with lcg^n(a) == b, else None"""
    s = a
    for n in range(limit + 1):
        if s == b:
            return n
        s = E.lcg(s)
    return None


def call_fields(c):
    b = E.decode(c['ent_before'])
    a = E.decode(c['ent_after'])
    diff = [(k, b[k], a[k]) for k in b if a[k] != b[k]]
    return b, a, diff


def hero_rel(c):
    """what the handlers read of the hero at the call: tile, floor, position, box / attack presence"""
    h = E.decode(c['hero_before'])
    return dict(tilex=h['tilex_120'], tiley=h['tiley_124'], floor=h['floor_134'], posx=h['posx'], posy=h['posy'], box=h['box_1D4'] != 0, atk=h['curattack_1C8'] != 0)


def call_line(c):
    b, a, diff = call_fields(c)
    n = draws_between(c['rng_in'], c['rng_out'])
    ch = ', '.join('%s %s->%s' % (k, _v(k, x), _v(k, y)) for k, x, y in diff)
    jr = ' ; '.join(_jfmt(j) for j in c['journal'])
    return 't=%4d slot %d %-3s letter %s | rng %08x -> %08x (%s draws) | %s%s' % (c['tick'], c['slot'], c['handler'], LETTERS[c['letter']] if c['letter'] is not None and 0 <= c['letter'] < 6 else '?',
                                                                                    c['rng_in'], c['rng_out'], '?' if n is None else n, ch or '(no change)', (' | journal: ' + jr) if jr else '')


def _v(k, v):
    if k in ('posx', 'posy', 'posz', 'floor_134', 'terrain_138', 'mh0_158', 'mh1_15C', 'mh2_160', 'mh3_164'):
        return px(v)
    if k in ('flags6C', 'curattack_1C8', 'box_1D4', 'touching_224'):
        return '0x%X' % (v & M32)
    return str(v)


def _jfmt(j):
    kind, a = j
    if kind == 'sound':
        return 'sound(%d)' % (a if not isinstance(a, list) else a[0])
    if kind == 'effect_3BE74':
        t = a[3]
        return 'effect_on_entity(id=%d, slot %s)' % (a[1], (t - ENT0) // STRIDE if ENT0 <= t < ENT0 + 64 * STRIDE else hex(t))
    if kind == 'effect_3BDD8':
        return 'effect(id=%d)' % a[1]
    if kind == 'spawn_39F58':
        return 'spawn(table=%d, sprite=%d)' % (a[1], a[2])
    return kind


# ---- facts -------------------------------------------------------------------------------------------------------------------------------
def fact_lines(res):
    L = []
    spec = res['spec']
    slots = res['meta']['slots']
    rows = [res['pre']] + res['rows']
    calls = res['calls']
    L.append('handler calls: %d' % len(calls))
    cnt = {}
    for c in calls:
        key = (c['handler'], c['letter'])
        cnt[key] = cnt.get(key, 0) + 1
    L.append('  by (handler, letter): ' + ', '.join('%s/%s x%d' % (k[0], LETTERS[k[1]] if k[1] is not None and 0 <= k[1] < 6 else '?', v) for k, v in sorted(cnt.items())))
    dr = {}
    for c in calls:
        n = draws_between(c['rng_in'], c['rng_out'])
        dr.setdefault(c['handler'], {}).setdefault(n, 0)
        dr[c['handler']][n] += 1
    L.append('  RNG draws per call (draws: calls): ' + '; '.join('%s {%s}' % (h, ', '.join('%s: %d' % (k, v) for k, v in sorted(d.items(), key=lambda kv: -1 if kv[0] is None else kv[0]))) for h, d in sorted(dr.items())))
    for i, (rid, slot) in enumerate(slots):
        T = lambda name: [r['targets'][i][name] for r in rows]
        tk = [r['tick'] for r in rows]

        def tl(name, cap=60):
            out = []
            prev = None
            for t, v in zip(tk, T(name)):
                if v != prev:
                    out.append('%d:%s' % (t, v))
                    prev = v
            return ' '.join(out[:cap]) + (' ...(%d more)' % (len(out) - cap) if len(out) > cap else '')
        L.append('target record %d (slot %d):' % (rid, slot))
        L.append('  current animation (tick:anim) ' + tl('anim_cur90'))
        L.append('  status ' + tl('status') + ' | hp ' + tl('hp'))
        L.append('  Bytes[0] ' + tl('b0_274', 24) + ' | Bytes[3] ' + tl('b3_277', 12))
        hits = [t for t, v, p in zip(tk[1:], T('c1d0')[1:], T('c1d0')[:-1]) if v == 0x19 and p != 0x19 + 0 and v > p]
        L.append('  hit pass wrote +0x1D0 = 0x19 at ticks %s' % hits)
        dcalls = [c['tick'] for c in calls if c['slot'] == slot and c['handler'] in ('D1', 'D9')]
        ecalls = [c['tick'] for c in calls if c['slot'] == slot and c['handler'] == 'E1']
        L.append('  D calls at ticks %s ; E1 at ticks %s' % (dcalls, ecalls))
        st4 = [t for t, v in zip(tk, T('status')) if v == 4]
        wipe = [t for t, v, r48 in zip(tk, T('status'), T('ref48')) if v == 0 and r48 == -1]
        L.append('  status 4 at ticks %s ; slot wiped (status 0, +0x48 -1) at ticks %s' % (st4[:3], wipe[:3]))
    # events (journal) -- break effect and loot, sounds
    ev = []
    for r in res['rows']:
        for kind, a in r['journal']:
            if kind in ('effect_3BE74', 'spawn_39F58'):
                ev.append('%d:%s' % (r['tick'], _jfmt((kind, a))))
    snd = {}
    for r in res['rows']:
        for kind, a in r['journal']:
            if kind == 'sound':
                snd.setdefault(a if not isinstance(a, list) else a[0], []).append(r['tick'])
    L.append('journalled effects on entities / spawns: ' + (' ; '.join(ev) if ev else 'none'))
    n0 = len(snd.pop(0, []))
    L.append('journalled sounds other than id 0 (id: ticks; id 0 = the animation pass asking for Sfx byte 0, %d calls): ' % n0 + ('; '.join('%d: %s' % (k, v[:8] + (['...'] if len(v) > 8 else [])) for k, v in sorted(snd.items())) if snd else 'none'))
    # probes
    pr = spec.get('probes') or []
    if pr:
        for rid in pr:
            ser = [(r['tick'], r['counts'][rid]) for r in res['rows']]
            tr = []
            prev = None
            for t, v in ser:
                if v != prev:
                    tr.append('%d:%d/%d' % (t, v[0], v[1]))
                    prev = v
            L.append('0x2C [%d] before each tick (tick:matches/Result): %s' % (rid, ' '.join(tr[:20])))
    hero = [r['hero'] for r in rows]
    hp = sorted(set(h['hp'] for h in hero))
    L.append('hero: hp values seen %s, c1cc poked to 0x78 each tick: %s' % (hp, spec.get('ghost', True)))
    L.append('uninitialised reads: %s' % (res['uninit'] or 'none'))
    return L


# ---- text ---------------------------------------------------------------------------------------------------------------------------------
COLS = [('status', 'st', 2), ('hp', 'hp', 3), ('anim_target88', 'at', 3), ('anim_cur90', 'ac', 3), ('dir_target8C', 'dt', 3), ('animdir98', 'ad', 2), ('ai0_280', 'ai0', 5), ('ai1_282', 'ai1', 5),
        ('b0_274', 'b0', 2), ('b1_275', 'b1', 3), ('b3_277', 'b3', 2), ('forceadj_13C', 'fa', 2), ('onground_144', 'og', 2), ('anim_end_AC', 'ae', 2), ('c1d0', 'c1d0', 4), ('hitcounter_220', 'hc', 2)]


def tick_lines(res, i):
    L = []
    head = 'tick hold rng      calls          | ' + ' '.join(n.rjust(w) for _, n, w in COLS) + ' | touch |    posx      posy      posz  | tile    | floor     mh0..3 (px)'
    L.append(head)
    for r in [res['pre']] + res['rows']:
        t = r['targets'][i]
        calls = ','.join('%s' % (c[1]) for c in r.get('calls', []) if c[0] == res['meta']['slots'][i][1])
        tch = (t['touching_224'] - ENT0) // STRIDE if t['touching_224'] else '-'
        L.append('%4d %04x %08x %-14s | ' % (r['tick'], r['hold'], r['rng'], calls) + ' '.join(str(t[k]).rjust(w) for k, _, w in COLS) + ' | %5s | %9s %9s %9s | %3d,%3d | %9s %s' % (
            tch, px(t['posx']), px(t['posy']), px(t['posz']), t['tilex_120'], t['tiley_124'], px(t['floor_134']), ' '.join(px(t[k]) for k in ('mh0_158', 'mh1_15C', 'mh2_160', 'mh3_164'))))
    return L


def hero_lines(res):
    L = ['tick | hero: hp  anim_c  dir_t    posx      posy   tile  floor_134  box atk  c1cc']
    for r in [res['pre']] + res['rows']:
        h = r['hero']
        L.append('%4d | %3d %6d %6d %9s %9s %3d,%3d %9s  %d  %d  %4d' % (r['tick'], h['hp'], h['anim_cur90'], h['dir_target8C'], px(h['posx']), px(h['posy']), h['tilex_120'], h['tiley_124'], px(h['floor_134']),
                                                                      1 if h['box_1D4'] else 0, 1 if h['curattack_1C8'] else 0, h['c1cc']))
    return L


def write_text(res, outdir, with_hero=True):
    spec = res['spec']
    L = ['# %s' % spec['name'], '# %s' % spec['desc'], '']
    L.append('setup: kind %s, map %s, hero start px %s, direction %s, seed 0x%08X, ticks %d, ghost hero %s, per-frame RNG advance %s, level D %s' % (
        spec['kind'], spec.get('map'), spec['hero_px'], spec['direction'], spec['seed'], spec['n'], spec.get('ghost', True), spec.get('frame_advance', False), res['meta']['level_D']))
    L.append('records spawned (record index, slot): %s ; bank: %s' % (res['meta']['slots'], res['meta']['bank']))
    for d in res['meta']['spawn']:
        nd = draws_between(d['rng_before'], d['rng_after'])
        L.append('spawn of record %d (slot %d): RNG %08x -> %08x (%s draws: the loot roll of InitializeEntity), ContentsItemId (+0x3C) %d, HP %d, anim %d, direction %d, Flags 0x%X, pos (%s, %s, %s) px' % (
            d['record'], d['slot'], d['rng_before'], d['rng_after'], nd, d['contents_item'], d['hp'], d['anim'], d['direction'], d['flags'], px(d['pos'][0]), px(d['pos'][1]), px(d['pos'][2])))
    L.append('inputs: pad holds %s ; harness events at ticks %s%s' % (spec['holds'], sorted(spec['before']), ('; sword policy ticks %s' % [(t, s_) for t, s_, _d in res.get('ctrl_log', [])][:40]) if res.get('ctrl_log') else ''))
    L.append('')
    L.append('== facts (computed from the rows) ==')
    L += fact_lines(res)
    L.append('')
    L.append('== one line per handler call (the call-by-call record: RNG word in and out, the entity fields that changed) ==')
    for c in res['calls']:
        L.append(call_line(c))
    for i in range(len(res['meta']['slots'])):
        L.append('')
        L.append('== target record %d slot %d: state AFTER the passes of each tick ==' % res['meta']['slots'][i])
        L += tick_lines(res, i)
    if with_hero:
        L.append('')
        L.append('== hero ==')
        L += hero_lines(res)
    path = os.path.join(outdir, spec['name'] + '.txt')
    with open(path, 'w', encoding='utf-8', newline='\n') as f:
        f.write('\n'.join(L) + '\n')
    return path


def run_json(res):
    spec = res['spec']
    rows = [res['pre']] + res['rows']
    d = dict(name=spec['name'], desc=spec['desc'], meta=res['meta'], tick=[r['tick'] for r in rows], hold=[r['hold'] for r in rows], rng=[r['rng'] for r in rows],
             latch=[r['latch'] for r in rows], hero={k: [r['hero'][k] for r in rows] for k in rows[0]['hero']},
             targets=[{k: [r['targets'][i][k] for r in rows] for k in rows[0]['targets'][i]} for i in range(len(res['meta']['slots']))],
             journal=[[r['tick'], r['journal']] for r in res['rows'] if r['journal']], counts=[[r['tick'], {str(k): v for k, v in r['counts'].items()}] for r in res['rows'] if r['counts']], calls=[])
    for c in res['calls']:
        b, a, diff = call_fields(c)
        d['calls'].append(dict(tick=c['tick'], handler=c['handler'], slot=c['slot'], letter=c['letter'], rng_in=c['rng_in'], rng_out=c['rng_out'], latch=c['latch'], v0=c['v0'],
                               before=b, after=a, hero=hero_rel(c), changed=[[k, x, y] for k, x, y in diff],
                               changed_words=[[o, x, y] for o, x, y in E.changed(c['ent_before'], c['ent_after'])],
                               hero_changed_words=[[o, x, y] for o, x, y in E.changed(c['hero_before'], c['hero_after'])], journal=c['journal']))
    return d


def write_json(res, outdir):
    path = os.path.join(outdir, res['spec']['name'] + '.json')
    with open(path, 'w', encoding='utf-8', newline='\n') as f:
        json.dump(run_json(res), f, separators=(',', ':'), sort_keys=True, default=str)
        f.write('\n')
    return path
