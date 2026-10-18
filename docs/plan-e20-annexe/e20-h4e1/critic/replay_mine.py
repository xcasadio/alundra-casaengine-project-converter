"""replay_mine.py - replay the h4e1 vectors (vec_wind1_80035EB0.json) on MY OWN interpreter (mymips.py), from an empty memory:
instruction fetch from the EXE, every data read must be in the vector's 'reads' (or be a byte written during the run), the final writes (outside 0x801FFE00..FF)
and v0 must equal the vector's. Hooks: SpawnWarpEntity 0x80039F58 returns stub_spawn_ret, DestroyEntityNoFx 0x8003A774 is a no-op returning 0.
usage: python -B replay_mine.py VECFILE"""
import json
import sys
import mymips as X

d = json.load(open(sys.argv[1]))
bad = 0
cov = set()
for v in d['vectors']:
    mem = X.Mem(strict=True)
    for (a, n, val) in v['reads']:
        mem.poke(a, n, val)
    spawn_log = []
    destroy_log = []

    def h_spawn(c, v=v, spawn_log=spawn_log):
        c.r[2] = v['stub_spawn_ret'] & X.M32
        spawn_log.append([c.r[4], c.r[5], c.r[6], c.r[7], c.m.rd(c.r[29] + 0x10, 4), c.m.rd(c.r[29] + 0x14, 4), c.m.rd(c.r[29] + 0x18, 4)])

    def h_destroy(c, destroy_log=destroy_log):
        destroy_log.append(c.r[4])
        c.r[2] = 0

    cpu = X.CPU(mem, hooks={0x80039F58: h_spawn, 0x8003A774: h_destroy})
    msg = None
    try:
        ret = cpu.run(0x80035EB0, sp=0x801FFF00)
    except X.Incomplete as e:
        msg = 'INCOMPLETE ' + str(e)
        ret = None
    if msg is None:
        fin = {}
        for (a, n, val) in mem.logw:
            if 0x801FFE00 <= a < 0x801FFF00:
                continue
            fin[(a, n)] = val
        want = {(a, n): val for (a, n, val) in v['writes']}
        if fin != want:
            msg = 'writes differ: got-only %s want-only %s' % ({k: fin[k] for k in fin if want.get(k) != fin[k]}, {k: want[k] for k in want if fin.get(k) != want[k]})
        elif (ret & X.M32) != v['ret']:
            msg = 'ret %#x != %#x' % (ret, v['ret'])
        elif v.get('spawn_calls') is not None and len(v['spawn_calls']) != len(spawn_log):
            msg = 'spawn count %d != %d' % (len(spawn_log), len(v['spawn_calls']))
        elif len(v.get('destroy_calls', [])) != len(destroy_log):
            msg = 'destroy count differs'
    cov |= cpu.executed
    if msg:
        bad += 1
        if bad <= 5:
            print('FAIL', v['id'], msg)
print('%d vectors replayed on my interpreter: %s ; distinct instruction addresses executed: %d' % (len(d['vectors']), 'all ok' if not bad else '%d FAIL' % bad, len(cov)))
sys.exit(1 if bad else 0)
