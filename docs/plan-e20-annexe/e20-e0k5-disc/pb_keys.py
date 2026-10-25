"""pb_keys.py - proposed new oracle keys for the point-blank slice (flush = hero at 109, wall column 5 ahead; near = hero at 105): written under t2-proj/f4f6bin/out/{traces,json}/ (a copy)."""
import os
import sys
HERE = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'f4f6bin')
sys.path.insert(0, HERE)
os.chdir(HERE)
import fw
from run_arrows import scen
from run_balls import ball, BALL_COLS
from run_charged import charged

WALL = fw.wall_col(5)
scen('arrow3_flush', 5, 50, hero_px=(109, 100), cells=WALL, title='Hunter arrow, hero flush against the wall (x=109, wall column 5)')
scen('arrow4_flush', 6, 50, hero_px=(109, 100), cells=WALL, title='William normal arrow, hero flush against the wall')
scen('arrow3_near105', 5, 50, hero_px=(105, 100), cells=WALL, title='Hunter arrow, hero at 105 (the 3 px step ends at 108, inside the window)')
charged('charged7_flush', 140, (1,), cells=WALL, hero_px=(109, 100), title='charged arrow, hero flush against the wall', first=100)
charged('charged7_near105', 140, (1,), cells=WALL, hero_px=(105, 100), title='charged arrow, hero at 105 (6 px step to 111: bounded to 109)', first=100)
ball('fire11_flush', 16, 50, hero_px=(109, 100), cells=WALL, title='fire ball, hero flush against the wall')
ball('fire12_flush', 16, 130, charged=True, hero_px=(109, 100), cells=WALL, title='charged fire ball, hero flush against the wall')
ball('ice14_flush', 14, 50, hero_px=(109, 100), cells=WALL, title='ice ball, hero flush against the wall')
ball('ice15_flush', 14, 130, charged=True, hero_px=(109, 100), cells=WALL, title='charged ice ball, hero flush against the wall')
