import sys, os
HERE = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, HERE)
import xyscen as SC
from xyscen import U
SUBJ = SC.ent_addr(1)
# oracle (t2-proj critic crit_sweep.txt): (first step raw, per-tick step raw) -> {x0: (xS_raw, adj@)}
ORA = {
 196608: {104:(7012352,3),106:(7143424,1),107:(7143423,2),108:(7143424,1),109:(7143424,0)},
 393216: {104:(7143423,2),106:(7143424,1),107:(7143423,2),108:(7143424,1),109:(7143424,0)},
}
bad=0
for first,tab in ORA.items():
    for x0,(xs_o,adj_o) in tab.items():
        w = SC.new_world(); SC.wall_columns(w, 5)
        SC.put(w, SUBJ, 0, (x0*U, 100*U, 1), d=24)
        r = SC.tick(w, SUBJ, first, 0); xs=r["x"]; fa = 0 if r["fa"] else None; k=0
        while fa is None:
            k+=1; r = SC.tick(w, SUBJ, 6*U if first==393216 else 6*U, 0)
            if r["fa"]: fa=k
        ok = (xs==xs_o and fa==adj_o); bad += (not ok)
        print(first, x0, "model", xs, fa, "oracle", xs_o, adj_o, "OK" if ok else "DIFF")
print("diffs", bad)
