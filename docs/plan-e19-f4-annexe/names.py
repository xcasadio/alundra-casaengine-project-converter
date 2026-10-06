"""All speaker names (ETC 0x100..0x1FF) from the exported Etc.yarn, with the REAL CalcTextWidth of the binary."""
import re, json, sys
from rig import *
ROOT = r'D:\development\repo\alundra-casaengine-project-converter'
def load_names():
    t = open(ROOT + r'\alundra-project\Dialogues\Etc.yarn', encoding='utf-8').read()
    d = {}
    for b in re.split(r'\n===\n', t):
        mt = re.search(r'title: Etc_(\d+)', b)
        if not mt: continue
        n = int(mt.group(1))
        if not (256 <= n < 512): continue
        body = b.split('---\n', 1)[1].strip()
        body = re.sub(r'\s*#line:\S+$', '', body)
        d[n] = body
    return d
if __name__ == '__main__':
    names = load_names()
    raw = {k: v.encode('cp1252') for k, v in names.items()}
    r = Rig(names=raw)
    out = {}
    for k in sorted(raw):
        w = r.m.call(0x8004771C, r.m.rw(ETC + 4 * k))
        w = w & 0xFFFFFFFF
        x = 64 + int((112 - w) / 2)
        out[k] = dict(name=names[k], w=w, x_rest=x, clipped_left=(x < 80), clip_px=max(0, 80 - x), right=x + w)
    for k, v in out.items():
        print(hex(k), v)
    print('max w', max(v['w'] for v in out.values()), 'min x', min(v['x_rest'] for v in out.values()))
    print('any clipped', [k for k, v in out.items() if v['clipped_left']])
    json.dump({hex(k): v for k, v in out.items()}, open('names_widths.json', 'w'), indent=1)
