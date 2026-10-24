"""tab.py - opcode dispatch table 0x80098FAC + 4*op of ALUN_CD.EXE; prints the handlers of the sound opcodes. Read-only."""
import sys, os
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), 'work'))
import exelib
base = 0x80098FAC
tab = [exelib.u32(base + 4 * i) for i in range(0xC5)]
for target in (0x80049634, 0x800490FC):
    pass
for op in (0xBC, 0xBD, 0xBE, 0xBF):
    print('op 0x%02X -> 0x%08X' % (op, tab[op]))
for op, a in enumerate(tab):
    if a in (0x8003FA9C, 0x80041C6C, 0x80041C38): print('op 0x%02X -> 0x%08X' % (op, a))
