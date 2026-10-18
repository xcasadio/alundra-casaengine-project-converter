from scen_lib import *
def trial(label, hold, nticks=40):
    m, STATS = mk(0x23)
    log = run(m, nticks, hold)
    started = [(r['t']) for r in log if any(x[1] == 'sound' and x[2] == 44 for x in r['jr'])]
    refused = [(r['t']) for r in log if any(x[1] == 'sound' and x[2] == 3 for x in r['jr'])]
    print('%-34s started at %s  refused(sound3) at %s  hero states %s' % (label, started, refused, sorted({r['hero']['tanim'] for r in log})))
trial('idle: Circle t5', lambda t: W.CIRCLE if t == 5 else 0)
trial('walking right: Circle t8', lambda t: (W.RIGHT | (W.CIRCLE if t == 8 else 0)) if t < 20 else 0)
trial('sword swing t2, Circle t4', lambda t: W.SQUARE if t == 2 else (W.CIRCLE if t == 4 else 0))
trial('sword swing t2, Circle t14', lambda t: W.SQUARE if t == 2 else (W.CIRCLE if t == 14 else 0))
trial('jump t2, Circle t6 (rising)', lambda t: W.CROSS if t == 2 else (W.CIRCLE if t == 6 else 0))
trial('run (dash) Circle', lambda t: (W.RIGHT) if t < 30 else 0)
