"""Compares what the C# probe rendered on the real GPU (out/*.png, out/scn_*.json, out/int_*.json) with the INDEPENDENT references (ref.py) built from the exported PNGs and the
binary's value tables. Prints a summary, writes results.md, the reference PNGs and the sample points table."""
import collections
import json
import os
import sys

import numpy as np
from PIL import Image

sys.dont_write_bytecode = True
import ref  # noqa: E402

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
OUT = os.path.join(ROOT, 'out')
PIX = os.path.join(ROOT, 'view', 'pixels')
os.makedirs(PIX, exist_ok=True)

A = ref.Assets()
allv = json.load(open(os.path.join(HERE, 'values_all.json'), encoding='utf-8'))
tables = {sid: {row['t']: row for row in v['table']} for sid, v in allv.items()}
states = json.load(open(os.path.join(HERE, 'states.json'), encoding='utf-8'))
lines = []


def say(s=''):
    print(s)
    lines.append(s)


def expected(st, k):
    c = ref.Canvas()
    if st.get('underlay', 'none') != 'none':
        ref.compose_textbox(c, A, ref.TEXTBOX_STATES[st['underlay']])
    ref.compose_choice(c, A, tables[st['scenario']][st['t']])
    img = c.scaled(k)
    off = st.get('offset')
    if off:
        ox, oy = off
        big = np.empty((240 * k + 2 * oy, 320 * k + 2 * ox, 3), dtype=np.uint8)
        big[:, :] = ref.BG
        big[oy:oy + 240 * k, ox:ox + 320 * k] = img
        img = big
    return img


# ---- 0. sanity of the compositor itself: the text box rest state equals the annex image of the binary-derived reference of f2b1 -----------------------------------------
say('## 0. The compositor against the binary-derived image of the f2b1 annex')
annex_rest = ref.load_rgb(r'D:\development\repo\alundra-casaengine-project-converter\docs\plan-e19-f2b1-annexe\pixels\rest.png')
c = ref.Canvas()
ref.compose_textbox(c, A, ref.TEXTBOX_STATES['rest'])
n, first = ref.diff(c.a, annex_rest)
say('- compose_textbox(rest) vs docs/plan-e19-f2b1-annexe/pixels/rest.png (x1): %d texels %s' % (n, first))

# ---- 1. the machine and the proposed view model against the binary's value tables ----------------------------------------------------------------------------------
say('')
say('## 1. Machine (real AlundraChoiceBox) and proposed view model, pass by pass, against the binary tables')
tot_rows = 0
bad_rows = []
for sid, s in states['scenarios'].items():
    recs = json.load(open(os.path.join(OUT, 'scn_%s.json' % sid)))
    tab = tables[sid]
    for r in recs:
        t = r['t']
        row = tab[t]
        tot_rows += 1
        vm = r['vm']
        want_drawn = bool(row.get('drawn'))
        problems = []
        if r['drawn'] != want_drawn:
            problems.append('drawn %s want %s' % (r['drawn'], want_drawn))
        if want_drawn:
            for mine, key in (('frameX', 'frame_x'), ('label0X', 'label0_x'), ('label1X', 'label1_x'), ('cursorX', 'cursor_x'), ('cursorImage', 'cursor_img'), ('selection', 'sel')):
                if r[mine] != row[key]:
                    problems.append('%s %s want %s' % (mine, r[mine], row[key]))
            if (r['frameY'], r['labelY'], r['cursorY']) != (144, 152, 136):
                problems.append('Y constants %s' % ((r['frameY'], r['labelY'], r['cursorY']),))
            # the view model
            if vm['root'] != 'Visible':
                problems.append('vm root %s' % vm['root'])
            if vm['frameLeft'] != row['frame_x']:
                problems.append('vm frameLeft %s' % vm['frameLeft'])
            if (vm['label0']['left'], vm['label1']['left']) != (row['label0_x'], row['label1_x']):
                problems.append('vm label lefts %s' % ((vm['label0']['left'], vm['label1']['left']),))
            if (vm['label0']['text'], vm['label1']['text']) != ('OUI', 'NON'):
                problems.append('vm label texts %s' % ((vm['label0']['text'], vm['label1']['text']),))
            if vm['cursor']['left'] != row['cursor_x'] or vm['cursor']['src'] != ref.CURSORS[row['cursor_img']]:
                problems.append('vm cursor %s' % (vm['cursor'],))
        else:
            if vm['root'] != 'Collapsed':
                problems.append('vm root %s while not drawn' % vm['root'])
        # sounds of the pass against the table (not the view's business, a free extra)
        if sorted(r['sounds']) != sorted(row.get('sfx', [])):
            problems.append('sounds %s want %s' % (r['sounds'], row.get('sfx')))
        if problems:
            bad_rows.append((sid, t, problems))
say('- %d passes over %d scenarios (V1 to V7 and V10): %d with a difference' % (tot_rows, len(states['scenarios']), len(bad_rows)))
for b in bad_rows[:10]:
    say('  - %s t=%d: %s' % b)

# ---- 2. static pixels ---------------------------------------------------------------------------------------------------------------------------------------------
say('')
say('## 2. Pixels of the proposed screen on this machine\'s GPU against the independent compositor')
groups = collections.OrderedDict()
failures = []


def group_of(st):
    sid = st['id']
    if sid.startswith('INV'):
        return 'control: windows added in the wrong order (must DIFFER)'
    if sid.startswith('U'):
        return 'choice over the text box (text box first, choice above)'
    if sid.endswith('_o'):
        return 'view offset (97, 41) in a larger target (window not 4:3)'
    if sid.endswith('_k3'):
        return 'x3 slide / rest'
    return 'choice alone, every pass of V1-V7, V10'


for st in states['states']:
    g = group_of(st)
    for k in st['ks']:
        suffix = '_off' if st.get('offset') else ''
        path = os.path.join(OUT, '%s_x%d%s.png' % (st['id'], k, suffix))
        got = ref.load_rgb(path)
        want = expected(st, k)
        n, first = ref.diff(got, want)
        e = groups.setdefault(g, dict(renders=0, exact=0, texels=0, maxd=0))
        e['renders'] += 1
        if n == 0:
            e['exact'] += 1
        else:
            e['texels'] += n
            e['maxd'] = max(e['maxd'], n)
            if not g.startswith('control'):
                failures.append('%s x%d: %d texels %s' % (st['id'], k, n, first))
for g, e in groups.items():
    say('- %s: %d renders, %d exact (0 texels), %d texels differ in total (largest %d)' % (g, e['renders'], e['exact'], e['texels'], e['maxd']))
for f in failures[:20]:
    say('  - FAIL ' + f)
say('- failures (non-control): %d' % len(failures))

# negative sensitivity control: a render against the reference of the NEXT pass must differ
ctl = []
for sid, t, sid2, t2 in (('V1', 5, 'V1', 4), ('V1', 5, 'V1', 6), ('V1', 25, 'V1', 24), ('V1', 19, 'V2', 22), ('V1', 19, 'V1', 21), ('V2', 22, 'V2', 24)):
    got = ref.load_rgb(os.path.join(OUT, '%s_t%02d_x1.png' % (sid, t)))
    n, _ = ref.diff(got, expected(dict(scenario=sid2, t=t2, underlay='none'), 1))
    ctl.append('%s t%d vs reference %s t%d: %d' % (sid, t, sid2, t2, n))
say('- sensitivity (a render against the reference of a neighbouring pass must differ): ' + '; '.join(ctl))

say('')
say('## 2b. Which XAML attributes carry the pixels (each removed alone from a copy of the proposed XAML; V1 N+19 and V2 N+22, x1, texels differing from the reference)')
import glob  # noqa: E402
names = sorted({os.path.basename(f)[len('VAR_'):].rsplit('_V', 1)[0] for f in glob.glob(os.path.join(OUT, 'VAR_*_V1_t19_x1.png'))})
for name in names:
    parts = []
    for sid, t in (('V1', 19), ('V2', 22)):
        path = os.path.join(OUT, 'VAR_%s_%s_t%02d_x1.png' % (name, sid, t))
        if not os.path.exists(path):
            parts.append('%s: render failed (see probe.log)' % sid)
            continue
        n, first = ref.diff(ref.load_rgb(path), expected(dict(scenario=sid, t=t, underlay='none'), 1))
        parts.append('%s t%d: %d texels%s' % (sid, t, n, (' (' + first + ')') if n else ''))
    say('- %s: %s' % (name, '; '.join(parts)))

# ---- 3. dynamic frames (the engine order: UI update, then the view model write, then draw) -------------------------------------------------------------------------
say('')
say('## 3. One-frame view lag (O-E19-65) measured on the choice: Update, then Apply, then Draw (no further update)')
for dyn in states['dynamic']:
    sid = dyn['scenario']
    tab = tables[sid]
    last = states['scenarios'][sid]['last_t']
    cls = collections.Counter()
    detail = []
    for t in range(1, last + 1):
        got = ref.load_rgb(os.path.join(OUT, '%s_t%02d_x1.png' % (dyn['id'], t)))
        row, prev = tab[t], tab.get(t - 1, {})

        def comp(r):
            c = ref.Canvas()
            ref.compose_choice(c, A, r)
            return c.a

        refs = collections.OrderedDict()
        refs['A: layout and sprite of this pass'] = comp(row)
        if prev.get('drawn') and row.get('drawn'):
            rb = dict(prev)
            rb['cursor_img'] = row['cursor_img']
            refs['B: layout of the previous pass, sprite of this one'] = comp(rb)
        refs['C: layout and sprite of the previous pass'] = comp(prev) if prev.get('drawn') else ref.Canvas().a
        hit = None
        for name, want in refs.items():
            if ref.diff(got, want)[0] == 0:
                hit = name
                break
        cls[hit or 'none'] += 1
        if hit is None or not hit.startswith('B'):
            detail.append('t%d -> %s' % (t, hit or 'none (%s)' % '; '.join('%s=%d' % (n[0], ref.diff(got, w)[0]) for n, w in refs.items())))
    say('- %s: ' % dyn['id'] + ', '.join('%s: %d' % kv for kv in cls.items()))
    for d in detail[:12]:
        say('    ' + d)

say('')
say('## 3b. The engine frame order with the real presenters (UI update, pass + presenters, draw): first push, second push, push after a cancel')


def bbox_of(img):
    bad = np.any(img != np.array(ref.BG), axis=2)
    ys, xs = np.nonzero(bad)
    return (int(xs.min()), int(ys.min()), int(xs.max()), int(ys.max()), int(bad.sum())) if len(xs) else None


for run_id in ('fo_a', 'fo_b', 'fo_c'):
    d = json.load(open(os.path.join(OUT, 'fo_%s.json' % run_id)))
    say('- %s: choice opened at tick %s%s%s; view events: %s' % (
        run_id, d['open1'], ', cancelled at tick %d' % d['cancelAt'] if d['cancelAt'] > 0 else '',
        ', second one opened at tick %d' % d['open2'] if d['open2'] > 0 else '',
        '; '.join('%d %s' % (e['tick'], e['ev']) for e in d['events'])))
    for label, open_at in (('first', d['open1']), ('second', d['open2'])):
        if open_at <= 0:
            continue
        # the cursor counter is never reset by the opener: the second choice starts at 35 after a complete OUI, at 9 after a cancel at N+10
        tab = tables['V1'] if label == 'first' else tables['V1_anim35' if run_id == 'fo_b' else 'V1_anim9']
        cls = collections.Counter()
        notes = []
        for rel in range(-1, 41):
            f = open_at + rel
            path = os.path.join(OUT, 'FO_%s_f%03d.png' % (run_id, f))
            if not os.path.exists(path):
                continue
            got = ref.load_rgb(path)
            if label == 'first' and d['cancelAt'] > 0 and rel > d['cancelAt'] - open_at + 1:
                break
            bb = bbox_of(got)
            if bb is None:
                cls['nothing drawn'] += 1
                if 3 <= rel <= 36 and tab[rel - 1].get('drawn') and tab[rel - 1]['frame_x'] < 320:
                    notes.append('rel %d: nothing drawn where the layout of the previous pass is on screen' % rel)
                continue
            ok = False
            if rel >= 3 and tab.get(rel - 1, {}).get('drawn') and tab.get(rel, {}).get('drawn'):
                rb = dict(tab[rel - 1])
                rb['cursor_img'] = tab[rel]['cursor_img']
                c = ref.Canvas()
                ref.compose_choice(c, A, rb)
                ok = ref.diff(got, c.a)[0] == 0
            if ok:
                cls['layout of the previous pass + sprite of this one'] += 1
            else:
                c = ref.Canvas()
                if rel >= 3 and tab.get(rel - 1, {}).get('drawn'):
                    ref.compose_choice(c, A, tab[rel - 1])
                same_prev = ref.diff(got, c.a)[0] == 0
                if same_prev:
                    cls['layout and sprite of the previous pass'] += 1
                else:
                    cls['other'] += 1
                    notes.append('rel %d (tick %d): drawn bbox x %d..%d y %d..%d, %d texels' % (rel, f, bb[0], bb[2], bb[1], bb[3], bb[4]))
        say('    %s open: %s' % (label, ', '.join('%s: %d' % kv for kv in cls.items())))
        for n in notes[:8]:
            say('      ' + n)

# ---- 4. the integrated run ---------------------------------------------------------------------------------------------------------------------------------------
say('')
say('## 4. Integrated run: the real director, the real text box presenter, the proposed choice presenter, a stand-in view doing what ScreenStack does')
for run in states['integrated']:
    d = json.load(open(os.path.join(OUT, 'int_%s.json' % run['id'])))
    tab = tables[run['table']]
    say('- %s: choice opened at tick %d, result %s at tick %d (N+%d)' % (run['id'], d['openAt'], d['result'], d['resultAt'], d['resultAt'] - d['openAt']))
    say('  view events: ' + '; '.join('tick %d %s %s (%d windows)' % (e['tick'], e['ev'], e['screen'], e['windows']) for e in d['events']))
    bad = 0
    for tk in d['ticks']:
        rel = tk['rel']
        if rel < 1 or rel not in tab:
            continue
        want_choice = bool(tab[rel].get('drawn'))
        has = 'choice' in tk['windows']
        if has != want_choice:
            bad += 1
    say('  the choice window is on the desktop exactly on the drawn passes N+2 .. : %d tick(s) differ over N+1 .. N+%d' % (bad, max(tab)))
    n_ok = n_bad = 0
    for rel in run['draw_at']:
        tk = next(t for t in d['ticks'] if t['rel'] == rel)
        c = ref.Canvas()
        ref.compose_textbox(c, A, tk['textVm'])
        if rel in tab and 'choice' in tk['windows']:
            ref.compose_choice(c, A, tab[rel])
        elif rel in tab and tab[rel].get('drawn'):
            say('  rel %d: the table draws the choice but its window is not on the desktop' % rel)
        got = ref.load_rgb(os.path.join(OUT, 'INT_%s_rel%02d_x1.png' % (run['id'], rel)))
        n, first = ref.diff(got, c.a)
        if n == 0:
            n_ok += 1
        else:
            n_bad += 1
            say('  rel %d: %d texels %s' % (rel, n, first))
    say('  pixels at N+%s: %d exact, %d differ' % (run['draw_at'], n_ok, n_bad))

# ---- 5. reference PNGs and sample points ------------------------------------------------------------------------------------------------------------------------
say('')
say('## 5. Reference images written under view/pixels/')
refs_to_save = [
    ('choice_V1_t19_rest_oui', dict(scenario='V1', t=19, underlay='none'), [1, 2]),
    ('choice_V2_t22_rest_non', dict(scenario='V2', t=22, underlay='none'), [1, 2]),
    ('choice_V1_t02_first_drawn', dict(scenario='V1', t=2, underlay='none'), [1, 2]),
    ('choice_V1_t05_slide_in', dict(scenario='V1', t=5, underlay='none'), [1, 2]),
    ('choice_V1_t25_slide_out', dict(scenario='V1', t=25, underlay='none'), [1, 2]),
    ('choice_V1_t34_slide_out_edge', dict(scenario='V1', t=34, underlay='none'), [1]),
    ('choice_V1_t36_off_screen', dict(scenario='V1', t=36, underlay='none'), [1]),
    ('choice_V1_t37_closed', dict(scenario='V1', t=37, underlay='none'), [1]),
    ('choice_V1_t01_init', dict(scenario='V1', t=1, underlay='none'), [1]),
    ('overlap_rest_V1_t19', dict(scenario='V1', t=19, underlay='rest'), [1, 2]),
    ('overlap_mrow_V1_t19', dict(scenario='V1', t=19, underlay='mrow'), [1, 2]),
    ('overlap_mrow_V2_t22', dict(scenario='V2', t=22, underlay='mrow'), [1]),
]
for name, st, ks in refs_to_save:
    for k in ks:
        Image.fromarray(expected(st, k)).save(os.path.join(PIX, '%s_x%d.png' % (name, k)))
say('- %d images (x1, x2) in view/pixels/' % sum(len(k) for _, _, k in refs_to_save))


def klass(rgb, inked=(41, 49, 16)):
    t = tuple(int(v) for v in rgb)
    if t == ref.BG:
        return 'bg (transparent texel shows the clear colour)'
    if t == inked:
        return 'ink'
    return 'frame/cursor colour'


points = ['# Sample pixel points (x1, native pixels; RGBA with A = 255), from the INDEPENDENT reference (ref.py + the binary tables)', '',
          'Each point is also checked in the GPU render by compare.py (the whole image is: 0 texels differ). "wrong" is what a plausible mistake would give there.', '']


def first_opaque(sprite_name):
    img, (x0, y0, w, h) = A.sprite(sprite_name)
    for y in range(h):
        for x in range(w):
            if img[y0 + y, x0 + x, 3] > 0:
                return x, y
    raise ValueError(sprite_name)


def first_ink(ch):
    g = A.fnt[ord(ch)]
    for y in range(g['height']):
        for x in range(g['width']):
            if A.page[g['y'] + y, g['x'] + x, 3] > 0:
                return x, y
    raise ValueError(ch)


def rgba(rgb):
    return '(%d, %d, %d, 255)' % tuple(int(v) for v in rgb)


for name, st in (('choice rest OUI (V1 N+19, active pass)', dict(scenario='V1', t=19, underlay='none')),
                 ('choice rest NON (V2 N+22, active pass)', dict(scenario='V2', t=22, underlay='none')),
                 ('choice slide-in (V1 N+5, frame x 282, cursor image 0)', dict(scenario='V1', t=5, underlay='none')),
                 ('choice slide-out (V1 N+25, frame x 224, cursor image 2)', dict(scenario='V1', t=25, underlay='none')),
                 ('choice over the text box at rest (V1 N+19 over "Bonjour, Alundra !")', dict(scenario='V1', t=19, underlay='rest')),
                 ('choice over a full first text row (V1 N+19 over 18 x the control glyph 0x1A, 14 px wide with ink on its first rows: the text passes under the choice frame)', dict(scenario='V1', t=19, underlay='mrow'))):
    row = tables[st['scenario']][st['t']]
    img = expected(st, 1)
    fx, l0, l1, cx = row['frame_x'], row['label0_x'], row['label1_x'], row['cursor_x']
    cimg = ref.CURSORS[row['cursor_img']]
    pts = []

    def add(x, y, why, wrong=None):
        if 0 <= x < 320 and 0 <= y < 240:
            pts.append((x, y, why, wrong))

    base_bg = ref.Canvas().a
    # the frame: its top-left corner texel is transparent (the clear colour, or the text box under it), its next ones are the beige border
    add(fx, 144, 'choice frame, top-left texel (a transparent texel of the baked cells: what is under shows)')
    add(fx + 2, 146, 'choice frame, border texel near the top-left')
    add(fx + 64, 160, 'choice frame, interior')
    add(fx + 127, 175, 'choice frame, bottom-right texel')
    # the labels
    var_path = os.path.join(OUT, 'VAR_no_label_padding_%s_t%02d_x1.png' % (st['scenario'], st['t']))
    var_img = ref.load_rgb(var_path) if os.path.exists(var_path) and st['underlay'] == 'none' else None
    for ch, lx, tag in (('O', l0, 'OUI'), ('N', l1, 'NON')):
        gx, gy = first_ink(ch)
        wrong = rgba(var_img[152 + gy, lx + gx]) if var_img is not None else None
        add(lx + gx, 152 + gy, 'first ink texel of "%s" (glyph %s, row-major; the label is at x %d, y 152, no padding)' % (tag, ch, lx),
            (wrong + ' with the default 1-pixel padding of a TextBlock (the glyphs one texel right and down)') if wrong else None)
    # the cursor: its first opaque texel, and the one in its middle
    ux, uy = first_opaque(cimg)
    add(cx + ux, 136 + uy, 'cursor %s, first opaque texel (row-major) at the cursor x %d, y 136' % (cimg, cx))
    add(cx + 8, 136 + 8, 'cursor %s, centre texel' % cimg)
    if st['underlay'] != 'none':
        # the texels where the order of the two windows matters: the choice window must be above the text box window
        inv = ref.Canvas()
        ref.compose_choice(inv, A, row)
        ref.compose_textbox(inv, A, ref.TEXTBOX_STATES[st['underlay']])
        differ = np.argwhere(np.any(inv.a != img, axis=2))
        if len(differ):
            for i in np.linspace(0, len(differ) - 1, min(5, len(differ))).astype(int):
                y, x = int(differ[i][0]), int(differ[i][1])
                add(x, y, 'order of the windows: the choice frame covers the text box here (text box windows above the choice would give the "wrong" colour)', rgba(inv.a[y, x]))
        add(fx + 4, 167, 'the row above the text box frame (y 168): still only the choice frame')
        add(fx + 4, 176, 'the first row under the choice frame: what the text box shows there (the choice frame ends at y 175)')
    if st['underlay'] == 'mrow':
        text_only = ref.Canvas()
        ref.compose_textbox(text_only, A, ref.TEXTBOX_STATES['mrow'])
        frame_only = ref.Canvas()
        ref.compose_textbox(frame_only, A, dict(ref.TEXTBOX_STATES['mrow'], row0=dict(text='', left=0, top=1), row1=dict(text='', left=0, top=17)))
        hidden = [(x, y) for y in range(173, 176) for x in range(176, 290) if tuple(text_only.a[y, x]) != tuple(frame_only.a[y, x]) and tuple(img[y, x]) != tuple(text_only.a[y, x])]
        if hidden:
            x, y = hidden[len(hidden) // 2]
            add(x, y, 'an ink texel of the first text row (y 173 to 175) that the choice frame covers', rgba(text_only.a[y, x]))
    points.append('## %s' % name)
    points.append('')
    points.append('| (x, y) | expected RGBA | class | what it pins | wrong |')
    points.append('|---|---|---|---|---|')
    seen = set()
    for x, y, why, wrong in pts:
        if (x, y) in seen:
            continue
        seen.add((x, y))
        rgb = img[y, x]
        points.append('| (%d, %d) | %s | %s | %s | %s |' % (x, y, rgba(rgb), klass(rgb), why, wrong or ''))
    points.append('')
open(os.path.join(ROOT, 'view', 'points.md'), 'w', encoding='utf-8').write('\n'.join(points))
open(os.path.join(ROOT, 'view', 'results.md'), 'w', encoding='utf-8').write('\n'.join(lines) + '\n')
print('written view/results.md and view/points.md')
