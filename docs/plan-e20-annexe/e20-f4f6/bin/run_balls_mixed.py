import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from run_balls import ball, cells_cols
# fire ball lands at x=166 (body corners at x=156 and x=176 -> columns 6 and 7): water only from column 7 -> mixed footprint
res, S = ball('fire11_mixed_footprint_water', 16, 60, cells=cells_cols(7, 9, (0, 0, 0, 0x08)), title='fire ball landing with half of its footprint on deep water (cols 7+): +0x18C is the MIN over the 4 corners -> 0 -> normal explosion')
res, S = ball('fire11_water_both_cols', 16, 40, cells=cells_cols(6, 9, (0, 0, 0, 0x08)), title='fire ball landing with the whole footprint on deep water (cols 6+): 18C=4')
