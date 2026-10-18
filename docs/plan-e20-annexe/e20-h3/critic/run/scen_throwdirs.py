"""scen_throwdirs.py - critic: throw range of the carried object in the four directions (hero turned by one tick of d-pad before Circle)."""
from scen_lib import *
for name, bit in (('RIGHT', W.RIGHT), ('LEFT', W.LEFT), ('UP', W.UP), ('DOWN', W.DOWN)):
    for item in (0x23, 0x1F):
        m, STATS = mk(item)
        def hold(t):
            if t == 1: return bit
            if t == 4: return W.CIRCLE
            if t == 50: return W.SQUARE
            return 0
        log = run(m, 120, hold)
        hx = hy = None; land = None
        for r in log:
            if r['t'] == 50:
                hx, hy = r['hero']['x'], r['hero']['y']
            thrown = [e for e in r['ents'].values() if e['sp'] == (2 if item == 0x23 else 1)]
            if r['t'] > 50 and thrown and land is None and thrown[0]['tanim'] == 2 and thrown[0]['z'] <= 0.01 or (r['t'] > 52 and thrown and land is None and thrown[0]['gnd'] and thrown[0]['tanim'] != 2):
                land = (thrown[0]['x'], thrown[0]['y'], r['t'])
        # landing = last position before status change; take the position at the tick where z hits 0 after the throw
        pos = None
        for r in log:
            if r['t'] > 51:
                th = [e for e in r['ents'].values() if e['sp'] == (2 if item == 0x23 else 1)]
                if th and th[0]['z'] <= 0.01:
                    pos = (th[0]['x'], th[0]['y'], r['t']); break
        print('%-5s item %02x  hero at throw (%.1f,%.1f)  object at first z=0: %s   delta (%.2f, %.2f)' % (name, item, hx, hy, pos, pos[0] - hx, pos[1] - hy))
