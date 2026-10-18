"""scen_cancels.py - critic: what the cancels do in the emulated game.  mode: lock4/lock10/lock20/lock34 (control lock bits), freeze08/freeze40 (world frozen), death (hero HP 0), exitcell (+0x188 & 0x80)"""
import sys
from scen_lib import *
STATE = 0x800DC4B8
def go(mode, item=0x23, t_event=60, nticks=130):
    m, STATS = mk(item)
    def on_tick(m, t, rec):
        if t == t_event:
            if mode.startswith('lock') or mode.startswith('freeze'):
                m.poke(STATE, int(mode[4:] if mode.startswith('lock') else mode[6:], 16))
            elif mode == 'death':
                m.poke(HERO + 0x14, 0)
            elif mode == 'exitcell':
                m.poke(HERO + 0x188, 0x80)
        if mode in ('lock20',) and t == t_event + 1:
            m.poke(STATE, 0)
    log = run(m, nticks, lambda t: (W.CIRCLE if t == 1 else 0), on_tick=on_tick)
    print('=== mode', mode, 'item', hex(item), 'event at tick', t_event)
    prev = None
    for r in log:
        if r['t'] < t_event - 2: continue
        key = ([(i, e['st'], e['tanim'], e['fuse'], e['mode']) for i, e in r['ents'].items()], r['hero']['tanim'], r['link'], r['item'], r['hero']['hp'])
        if key != prev or r['jr']:
            print(' t%3d' % r['t'], 'hero tanim %d link %x item %x hp %d' % (r['hero']['tanim'], r['link'], r['item'], r['hero']['hp']),
                  {i: (e['st'], e['tanim'], e['fuse'], e['mode'], round(e['z'], 1)) for i, e in r['ents'].items()}, [(x[1], x[2]) for x in r['jr']][:4])
        prev = key
if __name__ == '__main__':
    go(sys.argv[1], int(sys.argv[2], 16) if len(sys.argv) > 2 else 0x23, int(sys.argv[3]) if len(sys.argv) > 3 else 60)
