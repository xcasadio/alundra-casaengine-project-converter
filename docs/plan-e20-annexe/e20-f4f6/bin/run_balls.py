"""run_balls.py - f6 scenarios: fire (types 11, 12) and ice (types 14, 15) balls on the real code. Usage: python -B run_balls.py [name ...]
fire rod = item 16 (rec 4), ice rod = item 14 (rec 5).  Uncharged: Square pressed at tick 1 -> spawn S = P+11 = 12.  Charged: Square held 1..95, released 96 -> S = R+11 = 107.
"""
import sys
import os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import fw
import world as W
import c1world as C
from run_arrows import scen, save, first_tick, OUT, SQ, DIR, row_wall

HOLD95 = {t: SQ for t in range(1, 96)}
BALL_COLS = ['status', 'anim_t', 'dir_t', 'cnt1c', 'px', 'py', 'pz', 'fx', 'fy', 'fz', 'adj_13C', 'coll_140', 'ground_144', 'terr_138', 'slope_18C', 'reset_AC', 'hitc', 'touch_slot', 'c1d0', 'flags6C',
             'atk_attr', 'atk_pow', 'box1d4', 'hbx', 'hbz', 'bw', 'bd', 'bh']

SCENARIOS = {}


def sc(f):
    SCENARIOS[f.__name__] = f
    return f


def ball(name, item, n, charged=False, cells=None, targets=(), tall=None, direction=24, hero_px=(100, 100), title='', slots=None, first=1, cols=None, after_tick=None, init=None, seed=0x12345678):
    if tall is not None:
        C.map14().record['Header']['Ubuff'][13] = tall
    holds = dict(HOLD95) if charged else {1: SQ}
    spec = dict(item=item, n=n, holds=holds, after=0, hero_px=hero_px, direction=direction, cells=cells, targets=list(targets), seed=seed)
    if after_tick:
        spec['after_tick'] = after_tick
    if init:
        spec['init'] = init
    res = fw.run(spec)
    if tall is not None:
        C.map14().record['Header']['Ubuff'][13] = 8
    # the ball: the first slot holding a type in 10..15 after the press
    S = None
    cs = None
    for r in res['rows']:
        if charged and r['tick'] < 96:
            continue
        for s_, d in r['slots'].items():
            if 10 <= d['type68'] <= 15:
                S, cs = r['tick'], s_
                break
        if S:
            break
    sl = slots if slots else ([cs] if cs is not None else [])
    if slots is None and targets:
        sl = [cs] + [x for x in range(1, 1 + len(targets)) if x != cs]
    save(name, res, sl, rel=S, title='%s (ball spawn tick S=%s)  %s' % (name, S, title), first=first, cols=cols or BALL_COLS)
    print(name, 'S=', S, 'slot', cs)
    return res, S


def cells_cols(c0, c1, val, rows=range(0, 60)):
    rows = set(rows)

    def f(col, row):
        return val if (c0 <= col <= c1 and row in rows) else (0, 0, 0, 0)
    return f


# cell tuple = (height byte, slope byte, walkability, ground property)
@sc
def fire11_flat():
    return ball('fire11_flat', 16, 70, title='fire rod uncharged, east, flat field')


@sc
def ice14_flat():
    return ball('ice14_flat', 14, 70, title='ice rod uncharged, east, flat field')


@sc
def fire12_flat():
    return ball('fire12_flat', 16, 160, charged=True, title='fire rod CHARGED (type 12), east, flat field', first=96)


@sc
def ice15_flat():
    return ball('ice15_flat', 14, 215, charged=True, title='ice rod CHARGED (type 15), east, flat field', first=96)


@sc
def balls_dirs():
    out = []
    for d in ('west', 'north', 'south'):
        out.append(ball('fire11_%s' % d, 16, 60, direction=DIR[d], title='fire ball %s, flat field' % d))
    return out


@sc
def fire11_wall():
    out = []
    out.append(ball('fire11_wall_col6', 16, 80, cells=fw.wall_col(6), title='fire ball, wall column 6 (x 144..167, walk flag 0x40, height 0): hits the wall in the air'))
    out.append(ball('fire11_wall_col8', 16, 80, cells=fw.wall_col(8), title='fire ball, wall column 8 (x 192..215): the ball lands first (x=166)'))
    out.append(ball('ice14_wall_col6', 14, 80, cells=fw.wall_col(6), title='ice ball, wall column 6'))
    return out


@sc
def fire11_steps():
    out = []
    out.append(ball('fire11_step16_col6', 16, 80, cells=cells_cols(6, 9, (1, 0, 0, 0)), title='fire ball, 16 px step (height byte 1) from column 6 on (x>=144)'))
    out.append(ball('fire11_step32_col6', 16, 80, cells=cells_cols(6, 9, (2, 0, 0, 0)), title='fire ball, 32 px step (height byte 2) from column 6 on'))
    out.append(ball('fire11_step16_col7', 16, 80, cells=cells_cols(7, 9, (1, 0, 0, 0)), title='fire ball, 16 px step from column 7 on (x>=168)'))
    out.append(ball('ice14_step16_col6', 14, 80, cells=cells_cols(6, 9, (1, 0, 0, 0)), title='ice ball, 16 px step from column 6 on'))
    out.append(ball('ice14_step32_col6', 14, 80, cells=cells_cols(6, 9, (2, 0, 0, 0)), title='ice ball, 32 px step from column 6 on'))
    return out


@sc
def balls_water():
    out = []
    # landing cell of the fire ball (x=166 -> column 6); make columns 5..8 ground-property cells
    out.append(ball('fire11_gp8_deepwater', 16, 40, cells=cells_cols(5, 9, (0, 0, 0, 0x08)), title='fire ball landing on ground property 0x08 (18C=4, deep water)'))
    out.append(ball('fire11_gp2_slope1', 16, 40, cells=cells_cols(5, 9, (0, 0, 0, 0x02)), title='fire ball landing on ground property 0x02 (18C=1)'))
    out.append(ball('fire11_gp4_slope2', 16, 70, cells=cells_cols(5, 9, (0, 0, 0, 0x04)), title='fire ball landing on ground property 0x04 (18C=2): not a destroy value'))
    out.append(ball('fire11_gp6_slope3', 16, 70, cells=cells_cols(5, 9, (0, 0, 0, 0x06)), title='fire ball landing on ground property 0x06 (18C=3): not a destroy value'))
    out.append(ball('ice14_gp8_deepwater', 14, 40, cells=cells_cols(5, 9, (0, 0, 0, 0x08)), title='ice ball landing on ground property 0x08'))
    return out


@sc
def balls_target():
    out = []
    # the Beannoid (SizeZ 8) at x=156, y=104 (xpos 12, ypos 12); the fire ball falls from Z+32 -> the box is at Z 31..: compare with the real size 8 and the patched 32
    out.append(ball('fire11_vs_target', 16, 70, targets=[(12, 12, 0)], title='fire ball vs the real Beannoid at x=156,y=104 (slot 1 = target)'))
    out.append(ball('ice14_vs_target', 14, 70, targets=[(12, 12, 0)], title='ice ball vs the real Beannoid at x=156,y=104'))
    return out


@sc
def balls_target_far():
    # target after the landing point so only the explosion box reaches it: x=180 (xpos 14 -> 12*14+12 = 180)
    out = []
    out.append(ball('fire11_explosion_hits_target', 16, 80, targets=[(14, 12, 0)], title='fire ball lands at x=166; target at x=180 is reached by the explosion box only'))
    out.append(ball('ice14_explosion_hits_target', 14, 80, targets=[(14, 12, 0)], title='ice ball lands at x=163; target at x=180'))
    return out


@sc
def balls_charged_wall():
    out = []
    out.append(ball('fire12_wall_col6', 16, 160, charged=True, cells=fw.wall_col(6), title='fire CHARGED vs wall column 6 (the early normal ball dies on the wall before)', first=96))
    return out


def main():
    names = sys.argv[1:] or list(SCENARIOS)
    for n in names:
        SCENARIOS[n]()


if __name__ == '__main__':
    main()
