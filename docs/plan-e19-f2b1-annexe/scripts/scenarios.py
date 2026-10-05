"""Value tables for the f2b1 T1 test: the binary's drawn state per pass for six scenarios (production origin, corrected = D-E19-62/63).
Pass k = frame k of the model; the opening opcode runs in the tick N = 0 (frame 0), so the first pass is 1 (the text is typed from 19).
Writes scenarios.md and scenarios.json next to this file."""
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import model_drawn as D
import model as M


def widths_of(text_bytes):
    return M.calc_text_width(bytearray(text_bytes) + b'\0', 0)


def row_txt(r):
    return ''.join(M.glyph_char(g) for g in r['glyphs'])


def line(rec, which='rows_visible'):
    d = rec['drawn']
    if d is None:
        return '| %d | %s | not drawn | | | | | | |' % (rec['frame'], rec['phase'])
    rows = d[which]
    cells = []
    for r in rows:
        t = row_txt(r)
        cells.append(('"%s" x=%d y=%d' % (t, r['x'], r['y'])) if t else '-')
    cur = d['cursor']
    return '| %d | %s | %d | %d | %d..%d | %d | %s | %s |' % (
        rec['frame'], rec['phase'], d['y'], d['clip'][1], d['clip'][1], d['clip'][1] + d['clip'][3] - 1, d['offset'],
        ' / '.join(cells), ('img %d (u=%#x) at (%d,%d)' % (cur['image'], cur['u'], cur['x'], cur['y'])) if cur else '-')


HEADER = ('| pass | phase | frame Y | clip top | clip rows (inclusive) | scroll offset | rows as drawn (visible) x,y of each band | cursor |\n'
          '|---|---|---|---|---|---|---|---|')


def table(out, lo, hi, which='rows_visible'):
    s = [HEADER]
    for rec in out:
        if lo <= rec['frame'] <= hi:
            s.append(line(rec, which))
    return '\n'.join(s)


def changed(out, lo, hi):
    """Only the passes where the drawn rows / x / cursor / Y / clip / offset differ from the previous pass."""
    s = [HEADER]
    prev = None
    for rec in out:
        if not (lo <= rec['frame'] <= hi):
            continue
        d = rec['drawn']
        key = None if d is None else (d['y'], d['clip'], d['offset'], tuple((row_txt(r), r['x'], r['y']) for r in d['rows_visible']),
                                      None if d['cursor'] is None else d['cursor']['image'])
        if key != prev:
            s.append(line(rec))
        prev = key
    return '\n'.join(s)


md = []
js = {}


def scenario(name, text, pad, frames, desc):
    out, ctx, box = D.run_drawn(M.simple_script(text, mode=1) if not callable(getattr(text, '__call__', None)) else text, pad, frames, corrected=True)
    js[name] = [dict(frame=r['frame'], phase=r['phase'], drawn=r['drawn']) for r in out]
    md.append('\n## %s\n\n%s\n' % (name, desc))
    return out


# ---------------------------------------------------------------- S1: slide-in
out = scenario('S1 open and slide-in', b'AB', M.pad_none, 40,
               'Text "AB" (default flags, no pad). Opening opcode in tick N = 0. The frame Y is 240 on pass 1 (frame cells at y 240..295: off screen), the '
               'clip of pass 1 comes from the cfg.Y left by the previous release (static data: 168), NOT from 240 (0x800451E8-0x800452D8: InitializeDialogMessage '
               'never writes cfg.X/Y). First glyph pass 19.')
md.append(table(out, 1, 21))

# ---------------------------------------------------------------- S2: typing two lines
t2 = b'Bonjour, Alundra !\\Ndeuxi\xe8me ligne'
out = scenario('S2 typing a 2-line page', t2, M.pad_none, 600,
               'Text "Bonjour, Alundra !\\Ndeuxième ligne" (bytes: ... \\N d e u x i 0xE8 m e), default flags, no pad. A glyph every 4 passes from 19 '
               '(delay reset 4), the \\N is a step of its own. Y = 168, clip 172..221 at every pass after the slide. Passes where the visible rows change:')
md.append(changed(out, 17, 160))
js['S2 typed-done'] = [r['frame'] for r in out if 'typing-done' in r['notes']]
md.append('\nTyping done at pass %s; close timer 360 => close trigger 360 passes later (pass %s).\n' % (
    js['S2 typed-done'], [r['frame'] for r in D.run_drawn(M.simple_script(t2, mode=1), M.pad_none, 600, corrected=True)[0] if 'close-trigger' in r['notes']]))

# ---------------------------------------------------------------- S3: centred lines
s025 = b'\\B\\HRoue de la fortune !\\N\\HFlorin\\W5Roulette        '
s025_trim = b'\\B\\HRoue de la fortune !\\N\\HFlorin\\W5Roulette'
w0 = widths_of(b'Roue de la fortune !')
w1 = widths_of(b'Florin\\W5Roulette        ')
w1t = widths_of(b'Florin\\W5Roulette')
out = scenario('S3 centred lines (\\H)', s025, M.pad_none, 120,
               'Binary text of M472/473/474 S025: \\B\\HRoue de la fortune !\\N\\HFlorin\\W5Roulette + 8 trailing spaces. CalcTextWidth: row 0 = %d px '
               '-> x = 16 + (288 - %d)/2 = %d; row 1 = %d px (8 spaces x 4 = 32 included) -> x = %d. With the 8 spaces trimmed (what the Yarn export '
               'keeps, D-E15-8): row 1 = %d px -> x = %d (16 px off). The band x is final from the pass of the first glyph of the line (\\H is a free code '
               'taken in the same step). Passes where something changes:' % (
                   w0, w0, 16 + (288 - w0) // 2, w1, 16 + (288 - w1) // 2, w1t, 16 + (288 - w1t) // 2))
md.append(changed(out, 17, 120))
js['S3 widths'] = dict(row0=w0, row0_x=16 + (288 - w0) // 2, row1=w1, row1_x=16 + (288 - w1) // 2, row1_trimmed=w1t, row1_trimmed_x=16 + (288 - w1t) // 2)

# ---------------------------------------------------------------- S4: scroll ending
t4 = b'gypjq\\Ndeux\\Ntrois\\Nquatre'
out = scenario('S4 scroll (4 lines, no pad)', t4, M.pad_none, 200,
               'Text "gypjq\\Ndeux\\Ntrois\\Nquatre" (default flags, no pad). The \\N of line 3 arms the scroll (scrollMode 3: waits 10 passes, then 8 '
               'passes at offset 2, 4, ... 16). ScrollText only (no RenderText: no cursor). On the ending pass (offset 16, the pass that shifts bufX) the CPU '
               'draw list holds the OLD rows (rows_cpu below) but the ClearImage of the old top band (the band of row 0, 0x80045E30) runs before the OT is drawn '
               'so the visible state is the old rows 1 and 2 only, at the positions they keep after the shift (y 173 and 189).')
md.append(table(out, 78, 103))
ending = [r for r in out if r['drawn'] and r['drawn']['ending']][0]
md.append('\nEnding pass %d, CPU draw list (rows_cpu), before the ClearImage effect:\n\n%s\n' % (
    ending['frame'], '\n'.join('- row %d band %d "%s" x=%d y=%d' % (r['row'], r['band'], row_txt(r), r['x'], r['y']) for r in ending['drawn']['rows_cpu'])))
nxt = [r for r in out if r['frame'] == ending['frame'] + 1][0]
md.append('Next pass %d (interpreter again, shifted bands): %s\n' % (nxt['frame'], line(nxt)))

# ---------------------------------------------------------------- S5: slide-out
t5 = b'l1\\Nl2\\Ngypjq'
out = scenario('S5 slide-out (close by a press)', t5, M.pad_every_frame, 120,
               'Text "l1\\Nl2\\Ngypjq", Square held and just-pressed on every pass (the arcs\' pad): a glyph per pass from 19, then the press closes at once '
               '(close mode 3, bit 2). Y after the trigger: 168 (step 0), 172, 177, 182, 187, 192, 196, ... 235, 240, 240, then the closing pass '
               '(cfg restored to 168, DialogClosed: NOTHING drawn, not even the frame). The clip lags: it uses the cfg.Y of the previous pass.')
trig = [r['frame'] for r in out if 'close-trigger' in r['notes']]
last_drawn = [r['frame'] for r in out if r['drawn']][-1]
md.append('Close trigger pass %s; last drawn pass %d; DialogClosed pass %s.\n' % (trig, last_drawn, [r['frame'] for r in out if 'DialogClosed' in r['notes']]))
if trig:
    md.append(table(out, trig[0], trig[0] + 8))
    md.append('\n...\n')
    md.append(table(out, last_drawn - 1, last_drawn + 2))

# ---------------------------------------------------------------- S6: cursor
t6 = b'a\\Ab\\Ac'
press_a = lambda f, box: (f in (60, 120), f in (60, 120))
out = scenario('S6 cursor of \\A', t6, press_a, 200,
               'Text "a\\Ab\\Ac": \\A shows the cursor (no step cost beyond its own step) and waits for a press. The cursor tick is +1 before the image is '
               'chosen (u 0xB0 + 0x10 * (tick / 10), v 0x38; drawn at (288, Y + 32)): 9 passes of image 0 the first time, then 10 per image. '
               'The tick is not reset by the release: the second \\A continues the phase. Presses at pass 60 and 120. Passes where the cursor changes:')
md.append(changed(out, 17, 200))

open(os.path.join(HERE, 'scenarios.md'), 'w', encoding='utf-8').write('# f2b1 T1 value tables (generated by scenarios.py)\n' + '\n'.join(md) + '\n')
json.dump(js, open(os.path.join(HERE, 'scenarios.json'), 'w'), indent=1, default=str)
print('written')


# ---------------------------------------------------------------- CSV dump of every pass of every scenario
import csv
os.makedirs(os.path.join(os.path.dirname(HERE), 'values'), exist_ok=True)
for name, recs in js.items():
    if not (isinstance(recs, list) and recs and isinstance(recs[0], dict)):
        continue
    fn = os.path.join(os.path.dirname(HERE), 'values', name.split(' ')[0] + '.csv')
    with open(fn, 'w', newline='', encoding='utf-8') as fh:
        w = csv.writer(fh, delimiter=';')
        w.writerow(['pass', 'phase_after', 'drawn', 'kind', 'frameY', 'cfgYBefore', 'clipX', 'clipTop', 'clipW', 'clipH', 'offset', 'ending',
                    'row0', 'row0X', 'row0Y', 'row1', 'row1X', 'row1Y', 'row2', 'row2X', 'row2Y', 'cursorImage', 'cursorU', 'cursorX', 'cursorY'])
        for r in recs:
            d = r['drawn']
            if not d:
                w.writerow([r['frame'], r['phase'], 0] + [''] * 22)
                continue
            rows = []
            for x in d['rows_visible']:
                rows += [''.join(M.glyph_char(g) for g in x['glyphs']), x['x'], x['y']]
            c = d['cursor']
            w.writerow([r['frame'], r['phase'], 1, d['kind'], d['y'], d['cfg_y_before'], d['clip'][0], d['clip'][1], d['clip'][2], d['clip'][3], d['offset'],
                        int(d['ending'])] + rows + ([c['image'], hex(c['u']), c['x'], c['y']] if c else ['', '', '', '']))
print('csv written')
