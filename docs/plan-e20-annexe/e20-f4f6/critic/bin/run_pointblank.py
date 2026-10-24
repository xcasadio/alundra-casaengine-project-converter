import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import fw
import world as W
from run_balls import ball, BALL_COLS
from run_arrows import scen
r1 = scen('arrow3_point_blank', 5, 50, hero_px=(108, 100), cells=fw.wall_col(5), title='Hunter arrow shot with the hero 12 px from a wall (column 5, x>=120)')
r2 = ball('fire11_point_blank', 16, 50, hero_px=(108, 100), cells=fw.wall_col(5), title='fire ball shot with the hero 12 px from a wall (column 5, x>=120)')
