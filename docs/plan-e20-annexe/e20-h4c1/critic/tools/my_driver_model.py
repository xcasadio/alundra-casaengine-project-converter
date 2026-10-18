"""my_driver_model.py - OWN model (critic of h4c1) of the earth-1 driver 0x80035320 under the wrapper 0x8003634C and of E3 0x8007EDA0, written from MY disassembly
(lst_80035320.txt, lst_8003634C.txt, lst_E3.txt), table read from the executable by mydis.py.  Replays the 689 driver vectors and the 224 E3 vectors of the other agent."""
import json
import sys
from mydis import u16, s16

TABLE = [(u16(0x8002343C + 6 * i), s16(0x8002343C + 6 * i + 2), s16(0x8002343C + 6 * i + 4)) for i in range(33)]
assert TABLE[32][0] == 0xFFFF


def s32(x):
    x &= 0xFFFFFFFF
    return x - (1 << 32) if x & 0x80000000 else x


def driver(state):
    """state: dict phase, counter, anim, dir, x, y, z, slope, ret_entity, inuse.  Returns (ret, spawns, force_z, state)"""
    spawns, forcez = [], []
    ph = state['phase']
    ctr = state['counter']
    ret = 0

    def guard():
        if state['slope'] == 4 or ((state['anim'] - 0x32) & 0xFFFFFFFF) >= 2:
            state['inuse'] = 0              # 0x800350C0 -> table 0x800233EC -> 0x800351A0 (neutral) -> [0x80127164] = 0
            return 1
        return 0

    if ph == 1:
        g = guard()
        if g:
            ret = g
        elif ctr < 100:
            ret = 0
        else:
            state['anim'] = 0x34
            state['phase'] = ph + 1
            ret = 0
    elif ph == 0:
        g = guard()
        if g:
            ret = g
        else:
            if ctr >= TABLE[0][0]:               # slt v1, counter, first tick: skip the loop while counter < 20
                i = 0
                while True:
                    tick, vz, delta = TABLE[i]
                    if tick == ctr:
                        spawns.append(dict(a0='hero', a1=0, type=16, x=state['x'], y=state['y'], z=state['z'], dir=(state['dir'] + delta) & 31))
                        if state['ret_entity']:
                            forcez.append(s32(vz << 8))
                    i += 1
                    if not (ctr >= TABLE[i][0]):
                        break
                # after the loop: at the sentinel the phase moves to 1
                if TABLE[i][0] == 0xFFFF:
                    state['phase'] = ph + 1
            ret = 0
    elif ph == 2:
        ret = 0 if state['anim'] == 0x34 else 1
    else:
        ret = 0
    # wrapper 0x8003634C: counter increment after the call (saturates at 0x7FFFFFFF), item released when the driver returns non-zero
    if not (0x7FFFFFFE < state['counter']):
        state['counter'] += 1
    if ret != 0:
        state['inuse'] = 0
    return ret, spawns, forcez


def replay_driver(path):
    vecs = json.load(open(path))
    bad = 0
    for k, v in enumerate(vecs):
        i = v['inp']
        st = dict(phase=i['phase'], counter=i['counter'], anim=i['hero_anim'], dir=i['hero_dir'], x=i['hero_x'], y=i['hero_y'], z=i['hero_z'], slope=i['slope'],
                  ret_entity=i['spawn_returns_entity'], inuse=43)
        ret, spawns, forcez = driver(st)
        o = v['out']
        theirs_sp = [(s['type'], int(s['x']), int(s['y']), s['z'], s['dir']) for s in o['spawns']]
        mine_sp = [(s['type'], int(s['x']), int(s['y']), s['z'], s['dir']) for s in spawns]
        ok = (ret == o['ret'] and st['phase'] == o['phase'] and st['counter'] == o['counter'] and st['inuse'] == o['inuse'] and st['anim'] == o['hero_anim']
              and theirs_sp == mine_sp and forcez == o['force_z_written'])
        if not ok:
            bad += 1
            if bad <= 5:
                print('MISMATCH', k, i, o, dict(ret=ret, st=st, sp=mine_sp, fz=forcez))
    print('driver vectors:', len(vecs), 'mismatches vs my model:', bad)


def e3(inp):
    ta, ae, fl = inp['target_anim'], inp['anim_end'], inp['flags']
    destroy = []
    if ta == 1 and ae == 1:
        destroy.append(-1)
    else:
        ta = 1
        fl = ((fl | 0x40) & 0xFFFFFFCF) & 0xFFFFFFFF
    return dict(target_anim=ta, flags=fl, anim_end=ae, destroy=destroy)


def replay_e3(path):
    vecs = json.load(open(path))
    bad = 0
    for k, v in enumerate(vecs):
        m = e3(v['inp'])
        o = v['out']
        theirs_destroy = [d if isinstance(d, int) else d for d in o['destroy']]
        if not (m['target_anim'] == o['target_anim'] and m['flags'] == o['flags'] and m['anim_end'] == o['anim_end'] and len(m['destroy']) == len(theirs_destroy)):
            bad += 1
            if bad <= 5:
                print('E3 MISMATCH', k, v, m)
    print('E3 vectors:', len(vecs), 'mismatches vs my model:', bad)


if __name__ == '__main__':
    base = sys.argv[1]
    replay_driver(base + '/vec_driver_2B.json')
    replay_e3(base + '/vec_e3.json')
