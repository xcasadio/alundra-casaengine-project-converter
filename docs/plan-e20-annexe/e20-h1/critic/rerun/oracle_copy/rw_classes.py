"""rw_classes.py - which memory each function of the slice reads and writes, by class of address, over all the vectors ('game' and 'synthetic').  usage: python -B rw_classes.py"""
import glob, json, os, collections
HERE = os.path.dirname(os.path.abspath(__file__))
HERO = 0x80127D30
def cls(a):
    if HERO <= a < HERO + 0x294:
        return 'hero+0x%X' % (a - HERO)
    if 0x801E1000 <= a < 0x801E1010:
        return 'stats+%d' % (a - 0x801E1000)
    if 0x801E2000 <= a < 0x801E2400:
        return 'inventory[item].count' if (a - 0x801E2000) % 4 >= 2 else 'inventory[item].low'
    if 0x801E4000 <= a < 0x801E4300:
        return 'hero2+0x%X' % (a - 0x801E4000)
    if 0x801E8000 <= a < 0x801EA000:
        return 'effect+0x%X' % ((a - 0x801E8000) % 0x100)
    if a == 0x80098708:
        return 'rng(0x80098708)'
    if 0x80023654 <= a < 0x80023694:
        return 'EXE table 0x80023654 (x)'
    if 0x80023694 <= a < 0x800236D4:
        return 'EXE table 0x80023694 (y)'
    if 0x8002339C <= a < 0x800233EC:
        return 'EXE jump table 0x8002339C'
    names = {0x80176318: 'ptr stats', 0x8017631C: 'ptr inventory', 0x8017638C: 'inventory size', 0x80127164: 'in-use word', 0x8012715C: 'counter', 0x80127160: 'phase',
             0x800DC4B8: 'control flags', 0x80126F2A: 'pad pressed word'}
    return names.get(a & ~3, 'other 0x%08X' % a)
for f in sorted(glob.glob(os.path.join(HERE, 'vec', 'vec_*.json'))):
    d = json.load(open(f))
    R = collections.Counter(); W = collections.Counter()
    for v in d['vectors']:
        for a, n, x in v['reads']:
            R[cls(a)] += 1
        for a, n, x in v['writes']:
            W[cls(a)] += 1
    print(os.path.basename(f))
    print('   reads :', ', '.join(sorted(R)))
    print('   writes:', ', '.join(sorted(W)))
