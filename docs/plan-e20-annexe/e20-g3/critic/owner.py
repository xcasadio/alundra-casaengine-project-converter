"""Critic: for each address given, name the class table entries (A, C, D, E, F; 64 codes each) whose handler
is the nearest entry point at or below the address (read-only). Usage: python -B -I owner.py ADDR [ADDR ...]"""
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import cdis as X

MASTER = 0x80098F4C
entries = []
for slot in range(6):
    base = X.word(MASTER + 4 * slot)
    if base == 0:
        continue
    for code in range(256):
        try:
            h = X.word(base + 4 * code)
        except Exception:
            break
        if 0x80010000 <= h < 0x800C0000:
            entries.append((h, "ABCDEF"[slot] + str(code)))
entries.sort()
for a in sys.argv[1:]:
    a = int(a, 16)
    below = [e for e in entries if e[0] <= a]
    if not below:
        print(hex(a), "none")
        continue
    h = below[-1][0]
    names = [n for (x, n) in entries if x == h]
    print(f"{a:#x}: nearest handler entry {h:#x} = {','.join(names[:12])}{' ...' if len(names) > 12 else ''}")
