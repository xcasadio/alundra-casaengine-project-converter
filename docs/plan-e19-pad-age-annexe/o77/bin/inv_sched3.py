import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import inv_bin as S
from inv_sched import show
show('open, close at P+19, Start again at P+37', {0: S.START, 19: S.START, 37: S.START}, 70)
for q in (93, 94):
    show('open, L1 at P+19, L1 at P+56, Down at P+%d' % q, {0: S.START, 19: S.L1, 56: S.L1, q: S.DOWN}, 100)
show('open with L2 at P', {0: S.L2}, 22)
show('open with R2 at P', {0: S.R2}, 22)
show('Start+Select held at P (Select guard)', {0: S.START | S.SELECT}, 6)
show('Triangle at P (does not open)', {0: S.TRIANGLE}, 6)
show('open, Down held P+10..P+45 (repeat)', dict([(0, S.START)] + [(k, S.DOWN) for k in range(10, 46)]), 50)
