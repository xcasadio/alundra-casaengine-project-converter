"""Read-only MODEL summary (imports order_model.py of this folder): per liftable record, the share of whole-pixel Y phases where the carried object is in front of the hero, binary vs port,
over the carrying states (anim 5, 6, 7, 11, 12, 46, 47, 48 x 4 directions)."""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import order_model as M  # noqa: E402

states = [(a, d) for a in (5, 6, 7, 11, 12, 46, 47, 48) for d in range(4)]
print("record      collidable  objIDSV(anim0)   binary: in front (all states / min over states)    port: in front (mean / max over states)")
for bank in ("alundra_2", "alundra_1", "41", "107", "111", "112", "113", "182", "37", "48", "34", "35", "36", "38", "117", "137", "alundra_234"):
    if bank not in M.rec:
        continue
    col, rows = M.study(bank, states)
    if not rows:
        continue
    b = [r[5] for r in rows]
    p = [r[6] for r in rows]
    idsv = [M.idsv_frames(bank, 0, dd)[0] if M.idsv_frames(bank, 0, dd) else None for dd in range(4)]
    print("%-11s %-10s  %-14s   %5.3f / %5.3f                                        %5.3f / %5.3f" % (bank, col, idsv[0], sum(b) / len(b), min(b), sum(p) / len(p), max(p)))
