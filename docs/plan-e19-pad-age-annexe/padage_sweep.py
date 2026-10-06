import padage_model as pm

def run(mode, k, extra_right_at=None, ticks_after=120, tpf=1):
    """Press Cross at tick 0 (P), answer with a REAL pad Cross at tick k (OUI) - or Right at k then Cross at k+1 (NON) - 1-tick frames; returns
    (opener_tick, consumed_tick, picker_end_tick, answer taken: result) """
    w = pm.World(mode)
    total = k + ticks_after
    for g in range(total):
        hold = 0
        if g == 0:
            hold = pm.CROSS
        if extra_right_at is None:
            if g == k:
                hold = pm.CROSS
        else:
            if g == k:
                hold = pm.RIGHT
            elif g == k + 1:
                hold = pm.CROSS
        w.frame(1, hold)
    return w.opener_tick, w.consumed_tick, w.picker_end_tick, w.answer

print('earliest real-pad answer (Cross) in ticks after the opening press (P = tick 0); the screen takes the answer at tick T')
for mode in ('A', 'B0', 'B1'):
    for k in range(10, 30):
        o, c, e, ans = run(mode, k)
        if c is not None:
            print('  %-2s: opener at tick %s; earliest accepted press at P+%d -> taken at tick %s (P+%s), answer %s (1 = OUI), picker ends %s' % (mode, o, k, c, c, ans, e)); break
print('earliest NON (Right at k, Cross at k+1)')
for mode in ('A', 'B0', 'B1'):
    for k in range(10, 30):
        o, c, e, ans = run(mode, k, extra_right_at=k)
        if c is not None and ans == 2:
            print('  %-2s: opener at tick %s; earliest Right press at P+%d (Cross P+%d) -> taken at tick %s, answer %s, picker ends %s' % (mode, o, k, k + 1, c, ans, e)); break
