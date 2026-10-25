"""S8b: extra raw fields of the hero (and of the toucher) around the hurt, read from the emulated machine at the START of each tick (= end of the previous tick).
python -I s8b_extras.py <scenario> <nticks>"""
import sys, os
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, r'C:\Users\casad\AppData\Roaming\Python\Python314\site-packages')
ORA = os.path.join(HERE, 'oracle')
sys.path.insert(0, ORA)
sys.path.insert(0, os.path.join(ORA, 'tools'))
import c1world as C
import hworld as H
import hscen as HS

name = sys.argv[1] if len(sys.argv) > 1 else 'A1-pad0'
nmax = int(sys.argv[2]) if len(sys.argv) > 2 else 24
sp = dict(HS.find(name))
HERO = 0x80127D30
store = {}

def mk_hook(t):
    def hook(m, slots):
        h = {}
        for nm, off in (('Flags6C', 0x6C), ('Status10', 0x10), ('f20', 0x20), ('f2C', 0x2C), ('AnimFlagsB4', 0xB4), ('AnimSpeedF8', 0xF8), ('ForceMagB8', 0xB8),
                        ('Hazard180', 0x180), ('And184', 0x184), ('TileAttr188', 0x188), ('Slope18C', 0x18C), ('OnGround144', 0x144), ('HitBoxPtr1D4', 0x1D4), ('CurAtk1C8', 0x1C8),
                        ('Touch224', 0x224), ('HitCnt220', 0x220), ('AnimAC', 0xAC), ('AnimB0', 0xB0), ('AnimA8', 0xA8)):
            h[nm] = m.peek(HERO + off)
        tg = {}
        if slots:
            sl = slots[0]
            base = HERO + 0x294 * sl if isinstance(sl, int) else None
            if base is not None:
                for nm, off in (('Flags6C', 0x6C), ('CurAtk1C8', 0x1C8), ('HitBoxPtr1D4', 0x1D4), ('Hit220', 0x220), ('Cnt1D0', 0x1D0), ('Cnt1CC', 0x1CC)):
                    tg[nm] = m.peek(base + off)
                ap = m.peek(base + 0x1C8)
                tg['atk_attr'] = m.rb(ap) if ap else None
                tg['atk_power'] = m.rb(ap + 1) if ap else None
                tg['atk_bytes'] = [m.rb(ap + k) for k in range(0, 4)] if ap else None
        store[t] = (h, tg)
    return hook

sp['before'] = dict(sp.get('before') or {})
for t in range(1, nmax + 2):
    sp['before'][t] = mk_hook(t)
res = H.run(sp, 'emu', capture=False)
print("slots:", res['meta']['target_slots'])
for t in sorted(store):
    if t > nmax + 1:
        break
    h, tg = store[t]
    print("start of tick %2d: hero %s" % (t, {k: (hex(v) if isinstance(v, int) and v >= 16 else v) for k, v in h.items()}))
    if t <= 3:
        print("              toucher %s" % {k: (hex(v) if isinstance(v, int) and v is not None and v >= 16 else v) for k, v in tg.items()})
