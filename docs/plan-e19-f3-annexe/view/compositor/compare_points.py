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
                 ('choice over a full first text row (V1 N+19 over 25 x "m": the text passes under the choice frame)', dict(scenario='V1', t=19, underlay='mrow'))):
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
    for ch, lx, tag in (('O', l0, 'OUI'), ('N', l1, 'NON')):
        gx, gy = first_ink(ch)
        add(lx + gx, 152 + gy, 'first ink texel of "%s" (glyph %s, row-major: the label is at x %d, y 152, no padding)' % (tag, ch, lx),
            'one texel left or up if the label had the default 1-pixel padding')
        add(lx + gx - 1, 152 + gy - 1, 'the texel up-left of it: not ink (the default padding would put ink here)')
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
        add(fx + 4, 176, 'the first row under the choice frame: the text box frame alone (the choice frame ends at y 175)')
    if st['underlay'] == 'mrow':
        text_only = ref.Canvas()
        ref.compose_textbox(text_only, A, ref.TEXTBOX_STATES['mrow'])
        hidden = [(x, y) for y in range(173, 176) for x in range(176, 290) if tuple(text_only.a[y, x]) == (41, 49, 16)]
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
