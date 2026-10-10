"""sindex.py - E20.i discovery: INDEX.txt (one line per swim trace: file, ticks, start, pad pieces, layout, inventory, description) and manifest-swim.txt (md5 of every file of ../out-swim)
from the JSON traces rscen.write wrote.  NEW FILE.  usage: python -B sindex.py"""
import hashlib
import json
import os

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, '..', 'out-swim')
NAMES = [(0x1000, 'U'), (0x2000, 'R'), (0x4000, 'D'), (0x8000, 'L'), (0x10, 'T'), (0x20, 'O'), (0x40, 'X'), (0x80, 'S')]


def hold(h):
    return ''.join(n for b, n in NAMES if h & b) or '-'


def pads(p):
    if p == 'callable':
        return 'callable'
    return ' '.join('%d:%s' % (t, hold(h)) for t, h in p)


def layout(lay):
    if not lay:
        return 'flat'
    out = []
    for r in lay:
        w = (r['walk'] or 0) | ((r['gprop'] or 0) << 8) if (r['walk'] is not None or r['gprop'] is not None) else None
        out.append('c%d-%d,r%d-%d%s%s' % (r['c0'], r['c1'], r['r0'], r['r1'], (' word 0x%X' % w) if w is not None else '', (' hb %d' % r['height_byte']) if r['height_byte'] is not None else ''))
    return '; '.join(out)


def main():
    lines = ['# INDEX of the swim-oracle traces (one .txt and one .json per scenario, ../out-swim/<group>/; the .json is column-major, same format as the run oracle)',
             '# pad pieces: tick:hold (the hold word holds from that tick until the next piece); U R D L = d-pad, T O X S = Triangle Circle Cross Square',
             '# file | ticks | hero start px | start animation | pad | layout | targets | inventory | description']
    n = 0
    for root, dirs, files in os.walk(OUT):
        dirs.sort()
        for f in sorted(files):
            if not f.endswith('.json') or root.endswith('check'):
                continue
            d = json.load(open(os.path.join(root, f)))
            sp = d['spec']
            rel = os.path.relpath(os.path.join(root, f), OUT).replace(os.sep, '/')[:-5]
            lines.append('%s | %d | %s | %s | %s | %s | %s | %s | %s' % (rel, len(d['tick']) - 1, tuple(sp.get('hero_px', ())), '0x%02X' % sp.get('hero_anim', 0), pads(sp.get('pad_pieces', [])),
                                                                         layout(sp.get('layout')), sp.get('targets', '-') or '-', sp.get('inv', 'kit'), sp.get('desc', '')))
            n += 1
    open(os.path.join(OUT, 'INDEX.txt'), 'w', encoding='utf-8').write('\n'.join(lines) + '\n')
    ml = []
    for root, dirs, files in os.walk(OUT):
        dirs.sort()
        for f in sorted(files):
            if f.startswith('manifest'):
                continue
            p = os.path.join(root, f)
            ml.append('%s  %s' % (hashlib.md5(open(p, 'rb').read()).hexdigest(), os.path.relpath(p, OUT).replace(os.sep, '/')))
    open(os.path.join(OUT, 'manifest-swim.txt'), 'w').write('\n'.join(ml) + '\n')
    print(n, 'traces indexed;', len(ml), 'files in the manifest')


if __name__ == '__main__':
    main()
