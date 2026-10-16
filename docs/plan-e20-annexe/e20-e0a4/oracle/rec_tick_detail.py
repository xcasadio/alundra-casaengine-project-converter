"""rec_tick_detail.py - E20.e0a4 discovery, binary lens (scratch only): the pass-by-pass tables of the rows of out/rec_tick.json (written by rec_tick.py), k = -1 .. 4, plus the
creation snapshots (right after the first switch 0x80039EB0, after the world box 0x80039F38, after the opcode handler returned).  Reads only out/rec_tick.json.
usage: python -B rec_tick_detail.py [LABEL ...]   -> out/rec_tick_detail.txt (all rows when no label is given)"""
import os
import sys
import json

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, 'out')
PASSES = ['mapev', 'slots', 'events', 'counters', 'lists', 'anim', 'physics', 'effects', 'hits']
U = 65536


def px(v):
    return '%.1f' % (v / U) if v is not None else '-'


def fmt_snap(name, d):
    if not d:
        return '  %-14s none' % name
    return ('  %-14s tick %s pass %-12s status %d trig %d c1c %d cnt_A8 %d end_AC %d frame %s anim t/c %d/%d dir t/c %d/%d adir %d box1d4 %08X atk %08X animflags %02X | pos %s %s %s | mod %s %s %s | '
            'box x %s y %s z %s size %d x %d x %d | upd %s col %s draw %s count %d parent slot %s rec44 %08X'
            % (name, d.get('tick'), d.get('ps'), d['status'], d['trig'], d['c1c'], d['cnt_A8'], d['end_AC'], d['frame'], d['anim_t'], d['anim_c'], d['dir_t'], d['dir_c'], d['adir'],
               d['box1d4'], d['atk1c8'], d['animflags_B4'] & 0xFF, px(d['px']), px(d['py']), px(d['pz']), px(d['modx']), px(d['mody']), px(d['modz']),
               px(d['hbx']), px(d['hby']), px(d['hbz']), (d['bw'] + 1) >> 16, (d['bd'] + 1) >> 16, (d['bh'] + 1) >> 16, d['in_update'], d['in_collision'], d['in_draw'], d['count'],
               d['parent_slot'], d['rec44']))


def main():
    d = json.load(open(os.path.join(OUT, 'rec_tick.json')))
    want = set(sys.argv[1:])
    lines = []
    for r in d['rows']:
        if want and r['label'] not in want:
            continue
        S = r['S']
        lines.append('=' * 200)
        lines.append('%s  scenario %s op %s sprite %d  S = %s  birth pass %s  new slot %s  spawner slot %s  note: %s' % (r['label'], r['scenario'], r['op'], r['sprite'], S, r['birth_pass'],
                                                                                                          r['new_slot'], r['spawner_slot'], r['note']))
        lines.append('  stale slot state at the entry of InitializeEntity: %s' % r['stale_before_creation'])
        lines.append('  counter after the slot pass, per tick (S-1 .. S+2): %s' % {t: c for t, c in sorted((int(a), b) for a, b in r['count_after_slot_pass'].items()) if S - 1 <= t <= S + 2})
        lines.append(fmt_snap('after switch', r['creation']))
        lines.append(fmt_snap('after box', r['creation_end']))
        lines.append(fmt_snap('after opcode', r['after_op']))
        lines.append('  program turns of the new entity (tick, trigger, status, kind): %s' % [x for x in r['program_turns'] if x[0] <= S + 3])
        lines.append('  bytecode runner calls for the new entity (tick, slot, trigger): %s' % [x for x in r['interp_calls'] if x[0] <= S + 3])
        lines.append('  animation-pass calls %s  physics visits %s  hit-pass visits %s (first five)' % (r['anim_calls'][:5], r['physics_visits'][:5], r['hit_visits'][:5]))
        lines.append('  first: %s' % r['first'])
        lines.append('  %-4s %-9s | %-6s %-4s %-4s %-6s %-6s %-5s %-5s %-6s %-5s | %-3s %-3s %-4s %-5s | %-8s %-8s | %-8s %-8s %-8s %s' % (
            'k', 'pass', 'status', 'trig', 'c1c', 'cntA8', 'endAC', 'frame', 'animc', 'loops', 'adir', 'upd', 'col', 'draw', 'count', 'posx', 'modx', 'hbx', 'hby', 'hbz', 'size'))
        for x in r['ticks']:
            if x['k'] is None or x['k'] < -1 or x['k'] > 4:
                continue
            for nm in PASSES:
                p = x['passes'].get(nm)
                if not p:
                    continue
                lines.append('  %-4d %-9s | %-6d %-4d %-4d %-6d %-6d %-5s %-5d %-6d %-5d | %-3d %-3d %-4d %-5d | %-8s %-8s | %-8s %-8s %-8s %dx%dx%d' % (
                    x['k'], nm, p['status'], p['trig'], p['c1c'], p['cnt_A8'], p['end_AC'], p['frame'], p['anim_c'], p['loops_B0'], p['adir'], p['in_update'], p['in_collision'],
                    p['in_draw'], p['count'], px(p['px']), px(p['modx']), px(p['hbx']), px(p['hby']), px(p['hbz']), (p['bw'] + 1) >> 16, (p['bd'] + 1) >> 16, (p['bh'] + 1) >> 16))
        ck = r['clock']
        lines.append('  clock after the hit pass (k: call_no cnt1c cnt_A8 frame end_AC status hbx-px): ' + ' '.join(
            '%d:%d/%d/%d/%s/%d/%d/%s' % (c['k'], c['call_no'], c['cnt1c'], c['cnt_A8'], c['frame'], c['end_AC'], c['status'], px(c['hbx'] - c['px'])) for c in ck if c['k'] is not None and c['k'] <= 30))
    with open(os.path.join(OUT, 'rec_tick_detail.txt'), 'w', newline='\n') as f:
        f.write('\n'.join(lines) + '\n')
    print('\n'.join(lines))


if __name__ == '__main__':
    main()
