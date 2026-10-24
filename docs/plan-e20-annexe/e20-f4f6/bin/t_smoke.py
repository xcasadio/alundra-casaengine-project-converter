import sys
sys.path.insert(0, '.')
import fw, world as W
import time
t0 = time.time()
res = fw.run(dict(item=5, n=80, holds={1: W.SQUARE}, after=0, hero_px=(100, 100), cells=fw.wall_col(15)))
print('elapsed', time.time() - t0)
txt = fw.dump(res, 'out/traces/smoke_hunter_wall.txt', [1], only_changes=True, title='smoke')
print(txt[:6000])
