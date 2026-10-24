"""summarize.py - event table per scenario from out/json/*.json (tick of spawn S, ForceAdjusted, first status 3, ground, reset flag, status 4, slot free, hits, draws, effects, sounds)."""
import json, os, re, glob, collections
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'out')
lines = []
for fn in sorted(glob.glob(os.path.join(OUT, 'json', '*.json'))):
    name = os.path.basename(fn)[:-5]
    rows = json.load(open(fn))
    # projectile slot: first slot key that appears in the rows
    keys = []
    for r in rows:
        for k in r['slots']:
            if k not in keys:
                keys.append(k)
    if not keys:
        continue
    # choose the slot whose type68 is a projectile type
    pk = None
    START = 96 if (name.startswith('charged7') or name in ('fire12_flat', 'ice15_flat', 'fire12_wall_col6')) else 1
    for k in keys:
        for r in rows:
            if r['tick'] < START:
                continue
            d = r['slots'].get(k)
            if d and (d['type68'] in (3, 4, 7, 10, 11, 12, 13, 14, 15)):
                pk = k
                break
        if pk:
            break
    if pk is None:
        continue
    S = adj = st3 = gnd = rst = st4 = free = None
    hit = []
    ev = collections.OrderedDict()
    prev_present = False
    for r in rows:
        if r['tick'] < START:
            continue
        d = r['slots'].get(pk)
        if d is not None and S is None:
            S = r['tick']
        if d is not None:
            if adj is None and d['adj_13C']:
                adj = r['tick']
            if st3 is None and d['status'] == 3:
                st3 = r['tick']
            if gnd is None and d['ground_144']:
                gnd = r['tick']
            if rst is None and d['reset_AC'] == 1:
                rst = r['tick']
            if st4 is None and d['status'] == 4:
                st4 = r['tick']
            if d['hitc']:
                hit.append(r['tick'])
        if S is not None and d is None and prev_present and free is None:
            free = r['tick']
        prev_present = d is not None
    # events after S
    fx = collections.Counter()
    snd = collections.OrderedDict()
    draws = []
    for r in rows:
        if S is None or r['tick'] < S:
            continue
        for e in r['effects']:
            m = re.match(r'(fx|attach)\(id=(\d+)', e)
            if m:
                fx[(m.group(1), int(m.group(2)))] += 1
        for j in r['journal']:
            m = re.match(r'snd\((\d+)\)', j)
            if m and int(m.group(1)) != 0:
                snd.setdefault(int(m.group(1)), []).append(r['tick'])
        if r['draws']:
            draws.append((r['tick'] - S, r['draws']))
    rel = lambda t: None if t is None else t - S
    lines.append('%-34s S=%-4s adj=S+%s  E(status3)=S+%s  ground=S+%s  reset=S+%s  status4=S+%s  free=S+%s  hits=%s' % (name, S, rel(adj), rel(st3), rel(gnd), rel(rst), rel(st4), rel(free), [h - S for h in hit]))
    lines.append('      effects: %s | sounds(non-zero): %s | RNG draws (S+k:n): %s' % (dict(('%s%d' % k, v) for k, v in fx.items()), dict((k, [t - S for t in v]) for k, v in snd.items()), draws))
open(os.path.join(OUT, 'summary_events.txt'), 'w').write('\n'.join(lines) + '\n')
print('\n'.join(lines))
