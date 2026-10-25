import os,sys
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "g2c1"))
import os, sys
import c4_scen as S, gl, ana
gl.CFIELDS.append(('or180', 0x180, 0))
name = sys.argv[1]
for col in range(6, 10):
  for row in range(3, 6):
    cells = lambda cc, rr, col=col, row=row: dict(wk=0x04) if (cc, rr) == (col, row) else None
    m, c, rows = S.throw_run(name, cells=cells, throw_hold=gl.RIGHT, n=110)
    d = ana.first_tick(rows, lambda r: r['c'][c]['status'] == 0, first=62)
    ors = [(r['tick'], r['c'][c]['or180'], r['c'][c]['p184']) for r in rows if r['tick'] >= 80 and r['c'][c]['or180']][:3]
    land = rows[82]['c'][c]
    print(name, col, row, 'dead', d, 'or/and', ors, 'pos', land['px']/65536, land['py']/65536)
