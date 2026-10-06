import os
p = r'C:\Users\casad\AppData\Local\Temp\claude\D--development-repo-alundra-casaengine-project-converter\b00d1a72-420d-4acc-ab34-0c25fbdad3a1\scratchpad\f3b-disc\compositor\compare.py'
t = open(p, encoding='utf-8', newline='').read()

old_start = t.index("    for ch, lx, tag in (('O', l0, 'OUI'), ('N', l1, 'NON')):\n        gx, gy = first_ink(ch)")
old_end = t.index("    # the cursor: its first opaque texel")
new = '''    var_path = os.path.join(OUT, 'VAR_no_label_padding_%s_t%02d_x1.png' % (st['scenario'], st['t']))
    var_img = ref.load_rgb(var_path) if os.path.exists(var_path) and st['underlay'] == 'none' else None
    for ch, lx, tag in (('O', l0, 'OUI'), ('N', l1, 'NON')):
        gx, gy = first_ink(ch)
        wrong = rgba(var_img[152 + gy, lx + gx]) if var_img is not None else None
        add(lx + gx, 152 + gy, 'first ink texel of "%s" (glyph %s, row-major; the label is at x %d, y 152, no padding)' % (tag, ch, lx),
            (wrong + ' with the default 1-pixel padding of a TextBlock (the glyphs one texel right and down)') if wrong else None)
'''
t = t[:old_start] + new + t[old_end:]
open(p, 'w', encoding='utf-8', newline='').write(t)
print('patched')
