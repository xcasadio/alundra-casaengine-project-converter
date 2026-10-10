"""index.py - writes ../out/INDEX.txt: one line per scenario (trace file, number of ticks, hero start, pad pieces, dash override pieces, layout rectangles, targets, inventory, description).
The pad pieces are (from tick, hold word) with the bits Up 0x1000 Right 0x2000 Down 0x4000 Left 0x8000 Triangle 0x10 Circle 0x20 Cross 0x40 Square 0x80.
usage: python -B index.py"""
import glob
import json
import os

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'out')
NAMES = [(0x1000, 'U'), (0x2000, 'R'), (0x4000, 'D'), (0x8000, 'L'), (0x10, 'T'), (0x20, 'O'), (0x40, 'X'), (0x80, 'S')]


def hs(h):
    return ''.join(n for b, n in NAMES if h & b) or '-'


def main():
    lines = ['# INDEX of the run-oracle traces (one .txt and one .json per scenario; the .txt is the per-tick trace, the .json the same columns, column-major)',
             '# pad pieces: tick:hold (the hold word holds from that tick until the next piece); letters U R D L = d-pad, T O X S = Triangle Circle Cross Square',
             '# file | ticks | hero start px | pad | dash override | layout | targets | inventory | description']
    n = 0
    for f in sorted(glob.glob(os.path.join(OUT, 'g*', '**', '*.json'), recursive=True)):
        if os.sep + 'gate' + os.sep in f:
            continue
        d = json.load(open(f))
        sp = d['spec']
        rel = os.path.relpath(f, OUT).replace(os.sep, '/')[:-5]
        pad = sp.get('pad_pieces')
        pads = ' '.join('%d:%s' % (a, hs(b)) for a, b in pad) if isinstance(pad, list) else str(pad)
        dash = sp.get('dash_pieces')
        dashs = ' '.join('%d:%s' % (a, b) for a, b in dash) if isinstance(dash, list) else '-'
        lay = sp.get('layout')
        lays = '; '.join('c%d-%d r%d-%d%s%s%s%s' % (r['c0'], r['c1'], r['r0'], r['r1'],
                                                   (' hb%d' % r['height_byte']) if r['height_byte'] is not None else '', (' wk%02X' % r['walk']) if r['walk'] is not None else '',
                                                   (' gp%02X' % r['gprop']) if r['gprop'] is not None else '', '') for r in lay) if lay else 'flat'
        lines.append('%s | %d | %s | %s | %s | %s | %s | %s | %s' % (rel, len(d['tick']) - 1, tuple(sp['hero_px']), pads, dashs, lays, sp.get('targets', '-') or '-', sp.get('inv', 'kit'), sp.get('desc', '')))
        n += 1
    open(os.path.join(OUT, 'INDEX.txt'), 'w', encoding='utf-8').write('\n'.join(lines) + '\n')
    print(n, 'scenarios indexed')


if __name__ == '__main__':
    main()
