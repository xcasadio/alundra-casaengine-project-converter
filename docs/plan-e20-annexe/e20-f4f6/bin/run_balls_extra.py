import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import fw
import world as W
from run_balls import ball, BALL_COLS, cells_cols
from run_arrows import SQ

def cells(col, row):
    if col == 5:
        return (0, 0, 0, 0x08)          # deep water under the exploding ball (x 120..143)
    if col == 6:
        return (0, 0, 0x40, 0)          # wall
    return (0, 0, 0, 0)

res, S = ball('fire11_wall_then_water', 16, 70, cells=cells, title='fire ball explodes on wall col 6 in the air at x=133 and falls onto a deep-water cell (col 5): E9 branch A destroy(6) path')
# a pure-slope control: explode first on flat ground then step the fire ball? not possible; second case: ice
res, S = ball('ice14_wall_then_water', 14, 70, cells=cells, title='ice ball, same')
