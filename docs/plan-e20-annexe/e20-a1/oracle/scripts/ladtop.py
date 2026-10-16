"""ladtop.py - scratch probe (2026-10-16): what the REAL code of ALUN_CD.EXE (France) does when the hero climbing a ladder reaches the top.
Built on the E20.d run-oracle harness (rworld.py, copied unchanged).  Layout = rscen.ladder_layout (cliff hb 10 on rows <= 19, platform hb 2 on rows >= 20,
slope-6 strip word 0x0C00 on row 20, cols 2..20).  Every printed number comes from the emulated binary.
usage: python -B ladtop.py [scenario ...]
"""
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import rworld as R

OUT = os.path.join(HERE, '..', 'out-ladtop')
UP, RIGHT, DOWN, LEFT = R.UP, R.RIGHT, R.DOWN, R.LEFT
TRI, CRO = R.TRI, R.CRO
PX = 65536

# extra hero columns (read-only peeks)
R.HERO_COLS.append(('flags_6C', 0x6C, 0))
R.HERO_COLS.append(('row_124', 0x124, 1))
R.HERO_COLS.append(('col_120', 0x120, 1))
R.HERO_COLS.append(('lvl_128', 0x128, 1))


def ladder_layout(top_hb=10, low_hb=2, ladder=True, top_rows=19):
    lay = R.Layout().rect(0, 0, 51, top_rows, hb=top_hb).rect(0, top_rows + 1, 51, 59, hb=low_hb)
    if ladder:
        lay.rect(2, top_rows + 1, 20, top_rows + 1, word=0x0C00)
    return lay


def line(t, hold, h, j):
    return ('%4d %-4s | tgt %02X cur %02X b0=%d | dirT %02X | FX %7d FY %8d FZ %8d | X %7.2f Y %7.2f Z %7.2f | floor %7.2f terr %7.2f | g%d adj%d cz%d | sl%d | '
            'grav(6C&0x100)=%d flags %08X | cell c%d r%d lvl%d | %s') % (
        t, R.hold_str(hold), h['anim_target'] & 0xFF, h['anim_cur'] & 0xFF, h['b0_complete'], h['dir_target'] & 0xFF,
        h['force_x'], h['force_y'], h['force_z'], h['pos_x'] / PX, h['pos_y'] / PX, h['pos_z'] / PX,
        h['floor'] / PX, h['terrain'] / PX, h['on_ground'], h['force_adj'], h['coll_z'], h['slope'],
        1 if h['flags_6C'] & 0x100 else 0, h['flags_6C'], h['col_120'], h['row_124'], h['lvl_128'], j)


def run(name, desc, n, pad, layout, hero_px=(100, 392), direction=16, engine='emu', show_from=0):
    spec = dict(name=name, desc=desc, n=n, pad=pad, layout=layout, hero_px=hero_px, direction=direction)
    res = R.run_r(spec, engine)
    lines = ['# %s: %s  (engine %s)' % (name, desc, engine),
             '# columns: tick hold | TargetAnimationId(+0x88) current(+0x90) AnimCompleteCounter(+0xB0) | TargetDirection(+0x8C) | ForceX(+0xC4) ForceY(+0xC8) ForceZ(+0xB8) raw 16.16 | '
             'PosX/Y/Z (+0x114/+0x118/+0x11C, px, binary convention rest = floor... ) | floor(+0x134) terrain(+0x138) | IsOnGround(+0x144) ForceAdjusted(+0x13C) CollZ(+0x140) | slope(+0x18C) | '
             'flags +0x6C | +0x120 +0x124 +0x128 | journal']
    rows = [res['pre']] + res['rows']
    for r in rows:
        if r['tick'] < show_from and r['tick'] != 0:
            continue
        lines.append(line(r['tick'], r['hold'], r['hero'], R.fmt_items(r.get('journal', []))))
    lines.append('# uninit reads: %s' % res['meta'].get('uninit_reads'))
    txt = '\n'.join(lines) + '\n'
    os.makedirs(OUT, exist_ok=True)
    with open(os.path.join(OUT, name + ('' if engine == 'emu' else '.' + engine) + '.txt'), 'w', encoding='utf-8') as f:
        f.write(txt)
    with open(os.path.join(OUT, name + ('' if engine == 'emu' else '.' + engine) + '.json'), 'w', encoding='utf-8') as f:
        json.dump([dict(tick=r['tick'], hold=r['hold'], hero=r['hero']) for r in rows], f, separators=(',', ':'))
    return res, txt


SCEN = {
    # A: walk Up into the ladder strip and keep Up held until well after the top
    'A-up-held': lambda: run('A-up-held', 'Up held from tick 1, start (100,392) facing north, cliff hb10 (Z 160) above row 19, platform hb2 (Z 32)', 230, [(1, UP)], ladder_layout(), show_from=150),
    # B: Up held until the exit to Idle, then released (does the hero stand on the top or fall?)
    'B-release-at-exit': lambda: run('B-release-at-exit', 'Up held ticks 1..171, then nothing', 230, [(1, UP), (172, 0)], ladder_layout(), show_from=150),
    # C: lower cliff (hb 4 = Z 64) for a short climb, Up held
    'C-short-up-held': lambda: run('C-short-up-held', 'cliff hb4 (Z 64), platform hb2 (Z 32), Up held', 120, [(1, UP)], ladder_layout(top_hb=4), show_from=0),
}


def main(argv):
    cross = '--cross' in argv
    names = [a for a in argv if not a.startswith('--')] or list(SCEN)
    for nm in names:
        res, txt = SCEN[nm]()
        print(txt)
        if cross:
            spec = res['spec']
            res2 = R.run_r(spec, 'mips')
            mism = []
            for ra, rb in zip([res['pre']] + res['rows'], [res2['pre']] + res2['rows']):
                if ra['hero'] != rb['hero']:
                    mism.append(ra['tick'])
            msg = 'cross-check %s: %d ticks compared, mismatching ticks %s' % (nm, len(res['rows']) + 1, mism[:10])
            print(msg)
            with open(os.path.join(OUT, nm + '.cross.txt'), 'w', encoding='utf-8') as f:
                f.write(msg + '\n')


if __name__ == '__main__':
    main(sys.argv[1:])
