"""phase40.py - for the cases b40_phase_NN of probe_out_proto (map 40, the hero walks down into the block rec 0 from 818-k px, with the 0x72 prototype): the frame of the first contact,
the frame the block starts to slide and its total slide, to show that the slide is the remainder of the C program's 10-tick wait (it depends on the instant of the touch). Read-only."""
import re, os
HERE = os.path.dirname(os.path.abspath(__file__))
out = ['case          contact  slide(px)  frames-with-motion']
for k in range(12):
    n = 'b40_phase_%02d' % k
    rows = []
    for l in open(os.path.join(HERE, 'probe_out_proto', n + '.txt'), encoding='utf-8'):
        m = re.match(r'^\s*(\d+) (\S+)\s*\| H (\S+) (\S+) (\S+) a(\d+) d(\d+) f(\S+) ctl(\S+) xc=(\S+) adj(\d) \|(.*)$', l)
        if not m:
            continue
        r = re.search(r'\[rec0 (\S+) (\S+) (\S+) a(\d+)', m.group(12))
        rows.append((int(m.group(1)), float(r.group(2)), m.group(10)))
    contact = next(f for f, y, xc in rows if xc == '0')
    y0 = rows[0][1]
    steps = sum(1 for i in range(1, len(rows)) if rows[i][1] != rows[i - 1][1])
    out.append('%-13s f%-6d  %-9.1f  %d' % (n, contact, rows[-1][1] - y0, steps))
open(os.path.join(HERE, 'phase40_summary.txt'), 'w').write('\n'.join(out) + '\n')
print('\n'.join(out))
