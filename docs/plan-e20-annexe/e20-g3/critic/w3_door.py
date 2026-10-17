"""w3_door.py - critic: what the door of map 392 does to the hero's PosY (real code, my392 harness, 12 crates).
(a) ENTER: start (720, y0) facing west, hold LEFT 40 ticks; report PosY when the hero is first at x <= 690 (inside the door) and at the end (x, y).
    y0 from 286.0 to 295.0 by 0.0625 px.
(b) HUG: start (690, y0) in the door, hold UP 30 ticks then UP+LEFT 10 ticks; report the final PosY.
Usage: python -B -I w3_door.py"""
import os
import sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import multiprocessing as mp

U = 65536


def enter(y0):
    import my392 as G
    m, S = G.world((720, 296), 8, yraw=int(round(y0 * U)))
    rows = G.run(m, [(40, G.LEFT)], S)
    first = next(((r['tick'], r['hero']['py'] / U) for r in rows if r['hero']['px'] / U <= 690), None)
    last = rows[-1]['hero']
    return y0, first, last['px'] / U, last['py'] / U


def hug(y0):
    import my392 as G
    m, S = G.world((690, 312), 8, yraw=int(round(y0 * U)))
    rows = G.run(m, [(30, G.UP), (10, G.UP | G.LEFT)], S)
    return y0, rows[29]['hero']['py'] / U, rows[29]['hero']['px'] / U, rows[-1]['hero']['py'] / U, rows[-1]['hero']['px'] / U


if __name__ == '__main__':
    mp.freeze_support()
    ys = [286.0 + k * 0.0625 for k in range(int((295.0 - 286.0) / 0.0625) + 1)]
    with mp.Pool(22) as p:
        E = p.map(enter, ys)
        H = p.map(hug, [300.0, 310.0, 320.0, 328.0])
    out = ['ENTER from the east (hold LEFT): y0 -> (tick, PosY) at the first tick x <= 690 ; end x, y after 40 ticks']
    for y0, first, x, y in E:
        out.append('  y0 %.4f -> %s ; end x %.4f y %.7f%s' % (y0, None if first is None else '(%d, %.7f)' % first, x, y,
                   '' if first is None else ('  IN [295,297]' if 295.0 <= first[1] <= 297.0 else '  OUT')))
    inn = [e for e in E if e[1] and 295.0 <= e[1][1] <= 297.0]
    blocked = [e for e in E if e[1] is None]
    out.append('  summary: %d starts ; entered the door %d ; entered with PosY in [295, 297]: %d ; never reached x <= 690: %d' % (len(E), len(E) - len(blocked), len(inn), len(blocked)))
    out.append('HUG (start (690, y0), UP 30 ticks then UP+LEFT 10): y0 -> PosY, PosX after UP ; PosY, PosX after UP+LEFT')
    for r in H:
        out.append('  y0 %.1f -> %.7f, %.4f ; %.7f, %.4f' % r)
    txt = '\n'.join(out) + '\n'
    open(os.path.join(os.path.dirname(os.path.abspath(__file__)), 'out', 'w3_door.txt'), 'w').write(txt)
    print(txt)
