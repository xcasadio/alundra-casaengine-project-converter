"""Compares the real binary code (inv_bin.py) with the real proxy (probe-out/port-events*.txt), schedule by schedule.
Each event is keyed by the pad SAMPLE its pass consumed: binary RenderScene of frame f consumes sample f-1, binary Update(0) of frame f
consumes sample f; the port's tick k consumes TickPad sample k. Compared keys: flag6 (g_forbiddenWarpFlag), sel6, flag4 (g_subInventoryState),
sel4, post (g_postProcessState), MenuOpen, sounds 1/4/5."""
import sys, os, re
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import inv_bin as S
from inv_sched import events, P

def parse_port(path):
    sch = {}; cur = None
    for line in open(path, encoding='utf-8'):
        m = re.match(r'== (.*?)  presses\(rel P\): (.*)', line.rstrip())
        if m:
            cur = m.group(1); pr = {}
            for kv in m.group(2).split(', '):
                k, v = kv.split(':'); pr[int(k)] = int(v, 16)
            sch[cur] = (pr, []); continue
        m = re.match(r'\s+tick P([+-]\d+) \(TickPad sample P[+-]\d+\): (.*)', line)
        if m and cur:
            k, e = int(m.group(1)), m.group(2)
            if e.startswith('drawn'): continue
            e = e.replace('menuOpen 0->1', 'MenuOpen on').replace('menuOpen 1->0', 'MenuOpen off')
            sch[cur][1].append((k, e))
    return sch

def bin_events(presses, frames):
    rows = S.run(lambda f: presses.get(f - P, 0), P + frames)
    out = []
    for ph, f, e in events(rows):
        if e.startswith(('slot4', 'slot6')) or e in ('hud_init', 'icon_names', 'portrait_start', 'portrait_return', 'hud_before_hide', 'equip_weapon', 'equip_item'):
            continue
        e = e.replace('ctrl 0->8', 'MenuOpen on').replace('ctrl 8->0', 'MenuOpen off')
        out.append((f - 1 if ph == 'RS' else f, e))
    return out

if __name__ == '__main__':
    path = sys.argv[1]
    total = bad = 0
    for name, (pr, pev) in parse_port(path).items():
        frames = max(list(pr.keys()) + [k for k, _ in pev] + [0]) + 3
        bev = bin_events(pr, frames)
        horizon = frames - 3
        bset = sorted(set(x for x in bev if x[0] <= horizon)); pset = sorted(set(pev))
        total += 1
        if bset == pset:
            print('EQUAL     %s  (%d events)' % (name, len(pset)))
        else:
            bad += 1
            only_b = [x for x in bset if x not in pset]; only_p = [x for x in pset if x not in bset]
            print('DIFFERENT %s\n    binary only: %s\n    port only:   %s' % (name, only_b, only_p))
    print('%d schedules, %d different' % (total, bad))
