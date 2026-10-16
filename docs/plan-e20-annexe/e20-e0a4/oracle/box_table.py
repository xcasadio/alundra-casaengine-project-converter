"""box_table.py - E20.e0a4 discovery (scratch only): the frame box (world box +0x1FC.. relative to the position, size +0x214.. + 1 raw unit) at the creation and after the hit pass of
each tick k = 0 .. 10, plus the call number of the animation pass and the frame, for the rows named on the command line; reads out/rec_tick.json; writes out/box_table.txt."""
import os, sys, json
HERE = os.path.dirname(os.path.abspath(__file__))
U = 65536
rows = {r['label']: r for r in json.load(open(os.path.join(HERE, 'out', 'rec_tick.json')))['rows']}
want = sys.argv[1:] or ['REC-H1', 'REC-H1-8A', 'REC-H1-8B', 'REC-H2', 'REC-ME', 'REC-ME-8A', 'REC-LOAD', 'REC-SW0', 'REC-SW1']
out = []
for lab in want:
    r = rows[lab]
    c = r['creation_end']
    o = r['after_op']
    out.append('%s (S=%s, birth in %s): creation box (0x80039F30) has_box=%d  box-pos = (%.1f, %.1f, %.1f) size %dx%dx%d  pos (%.1f, %.1f, %.1f)%s' % (
        lab, r['S'], r['birth_pass'], 1 if c['box1d4'] else 0, (c['hbx'] - c['px']) / U, (c['hby'] - c['py']) / U, (c['hbz'] - c['pz']) / U, (c['bw'] + 1) >> 16, (c['bd'] + 1) >> 16, (c['bh'] + 1) >> 16,
        c['px'] / U, c['py'] / U, c['pz'] / U,
        ('' if not o or (o['px'], o['py'], o['pz']) == (c['px'], c['py'], c['pz']) else '  -> after the opcode: pos (%.1f, %.1f, %.1f), box unchanged (%.1f, %.1f, %.1f), mod unchanged (%.1f, %.1f, %.1f)' % (
            o['px'] / U, o['py'] / U, o['pz'] / U, o['hbx'] / U, o['hby'] / U, o['hbz'] / U, o['modx'] / U, o['mody'] / U, o['modz'] / U))))
    for x in r['clock']:
        if x['k'] is None or x['k'] < 0 or x['k'] > 10:
            continue
        out.append('    k=%-2d tick %-3d call_no %-2d frame %-2s cnt_A8 %-2d cnt1c %-3d status %d  box-pos (%.1f, %.1f, %.1f) size %dx%dx%d' % (
            x['k'], x['tick'], x['call_no'], x['frame'], x['cnt_A8'], x['cnt1c'], x['status'], (x['hbx'] - x['px']) / U, (x['hby'] - x['py']) / U, (x['hbz'] - x['pz']) / U,
            (x['bw'] + 1) >> 16, (x['bd'] + 1) >> 16, (x['bh'] + 1) >> 16))
open(os.path.join(HERE, 'out', 'box_table.txt'), 'w', newline='\n').write('\n'.join(out) + '\n')
print('\n'.join(out))
