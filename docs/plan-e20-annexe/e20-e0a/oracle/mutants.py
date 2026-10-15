"""mutants.py - E20.e0a1 T2: one-word mutants of the emulated binary (RAM patches, never saved) and the vectors of run_e0a.py that must see them.

Run from the repository root:   python -B docs/plan-e20-annexe/e20-e0a/oracle/mutants.py     (writes out/mutations.txt ; exit code 0 = every mutant CAUGHT)
Each mutant patches ONE instruction word of ALUN_CD.EXE in the RAM of a fresh machine (the old word is asserted), runs the whole battery of run_e0a.py (V1, V2, V3 incl. the raised terrain and the offset-Z prefabs, V5' with the
dirty slot, the invalid-index probes, V7) and is CAUGHT when the signature of the battery differs from the unmutated one (or the machine faults).  A mutant that survives is a missing vector, not a stop."""
import os
import sys
import hashlib
import json

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import espawn as E
import run_e0a as R

NOP = 0x00000000

# (label, address, old word, new word)
MUTANTS = [
    ('cap 64 instead of 63: slti 0x40 -> 0x41 (FindFreeEntitySlot 0x80039AFC)', 0x80039AFC, 0x28820040, 0x28820041),
    ('cap 62: the slot counter starts at 2 (0x80039AD8)', 0x80039AD8, 0x24040001, 0x24040002),
    ('occupancy test inverted: bnez -> beqz (0x80039AEC)', 0x80039AEC, 0x14400003, 0x10400003),
    ('root +0x08 store removed (0x80039D5C)', 0x80039D5C, 0xAE020008, NOP),
    ('parent +0x0C store removed (0x80039D44)', 0x80039D44, 0xAE05000C, NOP),
    ('serial counter store removed (0x80039D9C)', 0x80039D9C, 0xAC824600, NOP),
    ('serial +0x04 of the entity removed (0x80039DA0)', 0x80039DA0, 0xAE020004, NOP),
    ('+0x48 = -1 store removed, needs the dirty slot (0x80039D84)', 0x80039D84, 0xAE020048, NOP),
    ('+0x44 store removed, needs the dirty slot (0x80039D64)', 0x80039D64, 0xAE070044, NOP),
    ('Z - ModZ removed (0x80039EA0)', 0x80039EA0, 0x00431023, NOP),
    ('Z + 1 removed (0x80039EA4)', 0x80039EA4, 0x24420001, 0x24420000),
    ('terrain raise store removed (0x80039F04)', 0x80039F04, 0xAE04011C, NOP),
    ('terrain raise comparison inverted: bnez -> beqz (0x80039EF4)', 0x80039EF4, 0x1460000E, 0x1060000E),
    ('map page +0x100 of the identifier removed (0x80039FC4)', 0x80039FC4, 0x24C60100, 0x24C60000),
    ('+0x100 applied to the global page too: beqz -> bnez (0x80039FBC)', 0x80039FBC, 0x12600002, 0x16600002),
    ('page selection inverted in the lookup: beqz -> bnez (0x80039B2C)', 0x80039B2C, 0x10800006, 0x14800006),
    ('target direction +0x8C store removed (0x80039DC4)', 0x80039DC4, 0xAE09008C, NOP),
    ('complement of the direction +0x94 store removed (0x80039DB8)', 0x80039DB8, 0xAE020094, NOP),
    ('slot count word raise removed (0x80039D3C)', 0x80039D3C, 0xAC834360, NOP),
    ('palette +0x1B0 store removed (0x80039E1C)', 0x80039E1C, 0xAD42001C, NOP),
    ('sheet +0x1B4 store removed (0x80039E28)', 0x80039E28, 0xAD420020, NOP),
    ('self pointer +0x230 store removed (0x80042028)', 0x80042028, 0xAC840230, NOP),
    ('creation roll call removed (0x80039F38)', 0x80039F38, 0x0C00CA90, NOP),
    ('first animation switch call removed (0x80039EA8)', 0x80039EA8, 0x0C00E2AD, NOP),
    ('creation sound call removed (0x80038BC8)', 0x80038BC8, 0x0C01243F, NOP),
    ('hit points +0x14 store removed (0x80039E4C)', 0x80039E4C, 0xAE020014, NOP),
    ('maximum hit points +0x18 store removed (0x80039E44)', 0x80039E44, 0xAE020018, NOP),
    ('balance sheet pointer +0x1C4 store removed (0x80039E38)', 0x80039E38, 0xAE0201C4, NOP),
    ('flags +0x6C store removed (0x80039DD0)', 0x80039DD0, 0xAE04006C, NOP),
    ('program code A +0x70 store removed (0x80039DE0)', 0x80039DE0, 0xAE020070, NOP),
    ('identifier +0x68 store removed (0x80039D70)', 0x80039D70, 0xAE020068, NOP),
    ('status 1 store removed (0x80039D94)', 0x80039D94, 0xAE030010, NOP),
    ('lookup: negative index test removed (0x80039B5C)', 0x80039B5C, 0x04A00014, NOP),
    ('lookup: index below the count test removed (0x80039B6C)', 0x80039B6C, 0x00A2102A, 0x24020001),
    ('lookup: negative entry test removed (0x80039B8C)', 0x80039B8C, 0x04610009, NOP),
    ('lookup: upper bound of the entry removed (0x80039BA0)', 0x80039BA0, 0x14400004, NOP),
    ('failed lookup not tested in SpawnWarpEntity (0x80039FA0)', 0x80039FA0, 0x1220001F, NOP),
    ('full table not tested in SpawnWarpEntity (0x80039FB4)', 0x80039FB4, 0x1200001A, NOP),
]


class OldWordMismatch(Exception):
    pass


def patched(addr, old, new):
    def make(raised=0, map_no=14):
        w = R.make_world(raised, map_no)
        got = w.m.peek(addr)
        if got != old:
            raise OldWordMismatch('old word at 0x%08X is 0x%08X, expected 0x%08X' % (addr, got, old))
        w.m.poke(addr, new)
        return w
    return make


def main():
    base = R.signature(R.battery(R.make_world))
    lines = ['# E20.e0a1 T2: %d one-word mutants of the emulated binary against the battery of run_e0a.py (V1, V2, V3, V5\', probes, V7) ; baseline signature %s' % (len(MUTANTS), base),
             '# CAUGHT = the signature differs (or the machine faults) ; SURVIVED = a vector is missing']
    survived = 0
    for label, addr, old, new in MUTANTS:
        try:
            sig = R.signature(R.battery(patched(addr, old, new)))
            verdict = 'CAUGHT' if sig != base else 'SURVIVED'
        except OldWordMismatch:
            raise
        except Exception as e:
            verdict = 'CAUGHT (the machine faults: %s)' % type(e).__name__
        if verdict == 'SURVIVED':
            survived += 1
        lines.append('%-8s 0x%08X %08X -> %08X  %s' % (verdict, addr, old, new, label))
        print(lines[-1], flush=True)
    lines.append('%d mutants, %d CAUGHT, %d SURVIVED' % (len(MUTANTS), len(MUTANTS) - survived, survived))
    with open(os.path.join(R.OUT, 'mutations.txt'), 'w', encoding='utf-8', newline='\n') as f:
        f.write('\n'.join(lines) + '\n')
    print(lines[-1])
    return 0 if not survived else 1


if __name__ == '__main__':
    sys.exit(main())
