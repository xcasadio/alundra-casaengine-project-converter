import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import inv_bin as S
from inv_sched import show
show('open, L1 at P+19 (to sub-inventory)', {0: S.START, 19: S.L1}, 60)
for q in (55, 56, 57):
    show('open, L1 at P+19, Right at P+%d' % q, {0: S.START, 19: S.L1, q: S.RIGHT}, 62)
show('open, L1 at P+19, Start at P+56 (close sub)', {0: S.START, 19: S.L1, 56: S.START}, 80)
show('open, L1 at P+19, L1 at P+56 (back to main)', {0: S.START, 19: S.L1, 56: S.L1}, 120)
