import sys
import h3w
from h3w import *
ITEM = int(sys.argv[1], 0); NT = int(sys.argv[2]); SQ = int(sys.argv[3]); DIRHOLD = int(sys.argv[4], 0) if len(sys.argv) > 4 else 0
m, STATS, placed = S.make(item=ITEM, mp=4, mpmax=4, indices=(1, 2, 16, 17, 19, 20, 21, 22, 23, 24, 25))
holds = lambda t: (W.CIRCLE if t == 1 else 0) | (W.SQUARE if t == SQ else 0) | (DIRHOLD if t > 1 else 0)
run_trace(m, NT, holds, slots=(1, 2, 3), pos_every=4)
