from scen_lib import *
m, STATS = mk(0x23)
log = run(m, 300, lambda t: W.CIRCLE if t == 1 else 0)
prev = None
for r in log:
    h = r['hero']
    key = (h['hp'], h['tanim'])
    if key != prev and r['t'] > 240:
        print('t%3d hero hp %d tanim %d x %.2f y %.2f z %.2f' % (r['t'], h['hp'], h['tanim'], h['x'], h['y'], h['z']))
    prev = key
print('hero working sheet response bytes @0x80127008:', [m.peek8(0x80127008 + i) if hasattr(m,'peek8') else m.rb(0x80127008 + i) for i in range(0, 20)])
print('stats hp:', m.peek(STATS + 4) & 0xFFFF, m.peek(STATS + 6) & 0xFFFF)
