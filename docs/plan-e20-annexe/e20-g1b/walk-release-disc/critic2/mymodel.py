# independent transcription of ScriptedMotion.PlanForce/IncrementForce (open field, east, no ice/water), integers 16.16
def incr(f, t, s):
    if t != f:
        if f < t:
            f += s
            if t < f: return t
        else:
            f -= s
            if f < t: return t
    return f
def run(idle_acc, release_tick=10, n=13, east=768):
    sets = {1: (208, 1), 0: (0, idle_acc)}
    st = dict(speed=0, dir=None, acc=0, step=0, tgt=0, f=0, x=100*65536)
    cur_dir = 24
    out = []
    for t in range(1, n+1):
        anim = 1 if t < release_tick else 0
        speed, acc = sets[anim]
        if st['speed'] != speed or st['dir'] != cur_dir or st['acc'] != acc:
            tgt = east * speed
            st['step'] = abs(tgt - st['f']) >> acc
            st['tgt'] = tgt; st['speed'] = speed; st['dir'] = cur_dir; st['acc'] = acc
        st['f'] = incr(st['f'], st['tgt'], st['step'])
        st['x'] += st['f']
        out.append((t, st['x']/65536, st['f']/65536))
    return out
for acc in (1, 0):
    print('Idle acc=%d' % acc)
    for t, x, f in run(acc)[7:13]: print('  t%02d x=%.4f f=%.4f' % (t, x, f))
