"""D-E19-99: the real binary code (frameorder.py: RenderScene dispatcher then the real pad sampler, physical presses) against the host model in its three modes,
for OUI (Cross at P+q) and NON (Right at P+q, Cross at P+q+1), q = 15 .. 45, 1 press per schedule, P = the opening Cross."""
import padage_model as pm
import frameorder as fo

P = 5
CROSS, RIGHT = fo.CROSS, fo.RIGHT


def binary(schedule):
    phys = lambda f: schedule.get(f - P, 0)       # physical frame P + k
    m, rows = fo.run(phys, 140)
    o, f3, w, a = fo.milestones(rows)
    return (None if a is None else a - P)


def model(mode, schedule):
    w = pm.World(mode)
    for g in range(140):
        w.frame(1, schedule.get(g, 0))
    return w.consumed_tick


bad = {'A': 0, 'B0': 0, 'B1': 0}
n = 0
rows = []
for kind in ('OUI', 'NON'):
    for q in range(15, 46):
        sched = {0: CROSS}
        if kind == 'OUI':
            sched[q] = CROSS
        else:
            sched[q] = RIGHT
            sched[q + 1] = CROSS
        b = binary(sched)
        ms = {mode: model(mode, sched) for mode in ('A', 'B0', 'B1')}
        n += 1
        for mode in ms:
            if ms[mode] != b:
                bad[mode] += 1
        rows.append((kind, q, b, ms))
print('cases:', n, ' mismatches vs the binary (acceptance tick relative to the opening press):', bad)
for r in rows:
    if r[0] == 'OUI' and r[1] in (17, 18, 19, 20, 30) or r[0] == 'NON' and r[1] in (17, 18, 19, 20, 25):
        print(r)
