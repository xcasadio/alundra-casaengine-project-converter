"""q2_flagops.py - Q-G2C-2: in the maps that hold the 43 scripted trunks (72, 73, 79, 140, 158), every program (A to F, linear sweep) that executes 0x62/0x63 (set/clear flags low 16 bits),
0x16/0x17 (gravity), 0x28/0x29 (class B), 0x2A/0x2B (class A), 0x45/0x46: with its search type. READ-ONLY. Run: python -B -I -X utf8 q2_flagops.py"""
import sys
sys.path.insert(0, 'D:/development/repo/alundra-casaengine-project-converter/docs/plan-e20-annexe/e20-g3/dll')
import decode_events as de
OPS = {0x62: 'set flags', 0x63: 'clear flags', 0x16: 'gravity high', 0x17: 'gravity low', 0x28: 'classB on', 0x29: 'classB off', 0x2A: 'classA on', 0x2B: 'classA off', 0x45: 'slide on', 0x46: 'slide off', 0x41: 'sprite prog index'}
for m in (72, 73, 79, 140, 158):
    doc = de.load_map(m)
    print('== map', m)
    for slot, i, s, e in de.all_programs(doc):
        for pc, op, v, bad in de.sweep(doc, s, e):
            if op in OPS and v:
                st = v[1] if len(v) > 1 else -1
                print('   %s[%d] @%d 0x%02X %-14s %s%s' % (slot, i, pc, op, OPS[op], list(v), '' if st == 0x80 else '   <== search type %d (not self)' % st))
