import sys, os
HERE = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, os.path.join(HERE, 'oracle'))
import pworld as P
import world as W
m, meta = P.setup(item=None, hero_px=(100, 100), direction=24, targets=(), cells=None)
print(meta['ginfo'].keys() if meta else None)
print(hex(m.peek(0x80134360)))
