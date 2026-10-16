"""scan_anim0.py - statistics of animation set 0 (the animation a freshly created entity plays) over the global sprite bank (map_alundra.json SpriteRecords), read-only.
For each valid record and each of the 4 direction rows (down, up, left, right = adir 0..3): delay of frame 0, number of display frames, kind of terminator (end/hold = 0, loop = 1), whether frame 0 has a collision box."""
import sys, os, json, collections
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import pworld as P
recs = P.global_records()
rows = []
for i, r in enumerate(recs):
    if not r or not r.get('AnimSets'):
        continue
    s = r['AnimSets'][0]
    per = []
    for dr in range(4):
        an = s['PreloadedAnims'][dr] if dr < len(s['PreloadedAnims']) else None
        if an is None:
            per.append(None); continue
        fr = an['Frames']
        disp = [f for f in fr if not f['IsTransitionFrame']]
        term = [f for f in fr if f['IsTransitionFrame']]
        d0 = disp[0]['Delay'] & 0x7F if disp else None
        tk = None
        if term:
            t = term[0]
            tk = 'loop' if t['Delay'] == 1 else ('end/chain(byte1=%d)' % t['TransformIndexLow'] if t['Delay'] == 0 else 'other%d' % t['Delay'])
        per.append(dict(d0=d0, n=len(disp), total=sum(f['Delay'] & 0x7F for f in disp), term=tk, box0=bool(disp and disp[0].get('CollisionData'))))
    rows.append((i, s['Acceleration'], s['Sfx'], s['Unknown'], s['Flags'], per))
print('global sprites with an animation set 0:', len(rows))
c = collections.Counter()
for i, acc, sfx, unk, fl, per in rows:
    for dr, p in enumerate(per):
        if p:
            c[p['d0']] += 1
print('frame-0 delay distribution over (sprite, direction) pairs:', sorted(c.items()))
print()
for t in (3, 4, 7, 11, 12, 14, 15, 27, 28, 29):
    for i, acc, sfx, unk, fl, per in rows:
        if i == t:
            print('sprite %d: AnimFlags(+B4 <- byte 0xD)=0x%X sfx(byte 0xC)=%d impulse(+F8 <- s16 bytes 0xA-0xB)=%d' % (i, acc, sfx, ((fl << 8) | unk) - (0x10000 if fl & 0x80 else 0)))
            for dr, p in enumerate(per):
                print('    dir row %d: %s' % (dr, p))
print()
print('sprites whose frame-0 delay is 1 in at least one direction (the first anim-pass call would advance to frame 1 in the birth tick):')
print([i for i, acc, sfx, unk, fl, per in rows if any(p and p['d0'] == 1 for p in per)])
