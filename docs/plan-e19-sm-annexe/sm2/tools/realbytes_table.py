"""Writes the expected-values table of the real-bytes tests (port) and their red values (today) in a compact, copy-ready form.
python -B realbytes_table.py > ../proposed/RealBytesExpected.txt"""
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import mini_prog as P
import scenarios as S
import corpus2 as C

M32 = 0xFFFFFFFF


def hx(v):
    return '0x%08X' % (v & M32)


print('# Real-bytes tests (E19.sm2 T1): expected values (port) and red values (today = the skip by size). Produced by the second-method interpreter')
print('# tools/mini_prog.py from the REAL exported programs; positions are 16.16 words; "executed" lists (pc:opcode) the instructions the runner dispatches in the call')
print('# (the End/Break records come last). Result0 is the Result the state holds before the first tick. start = CodeIndex of the first call (entry unless said).')
print()
for name, mid, slot, prog, start, result0, ticks in S.SCENARIOS:
    codes, entry = P.program(mid, slot, prog)
    st0 = entry if start is None else start
    print('=== %s' % name)
    print('    map %d, program %s[%d], entry %d, first call at CodeIndex %d, Result0 %d, all other Parameters 0, flags all clear, no host (bare runner)' % (mid, slot, prog, entry, st0, result0))
    port = P.simulate('port', mid, slot, prog, start, result0, ticks)
    today = P.simulate('today', mid, slot, prog, start, result0, ticks)
    for i, ((ex, res, ci, memo, bud, pdir), (tex, tres, tci, tmemo, tbud, tdir), t) in enumerate(zip(port, today, ticks), 1):
        pos = t['pos']
        print('  tick %d  pos=(%s, %s, %s)' % (i, hx(pos[0]), hx(pos[1]), hx(pos[2])))
        print('     port : executed %-70s Result=%d CodeIndex=%d P2..P4=(%s) TargetDirection=%d' % (' '.join('%d:%02X' % e for e in ex), res, ci, ', '.join(hx(m) for m in memo), pdir))
        red = (ex != tex) or res != tres or ci != tci or memo != tmemo or pdir != tdir
        unmodelled = sorted({'%02X' % op for pc, op in list(ex) + list(tex) if op in (0x6F, 0x3E, 0x36)})
        print('     today: executed %-70s Result=%d CodeIndex=%d P2..P4=(%s) TargetDirection=%d  %s%s' % (' '.join('%d:%02X' % e for e in tex), tres, tci, ', '.join(hx(m) for m in tmemo), tdir, 'RED' if red else 'same',
              '  [opcode(s) %s write Result in the DLL, not modelled here: assert only the ticks before them]' % ','.join(unmodelled) if unmodelled else ''))
    print()
