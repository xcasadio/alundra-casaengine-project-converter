"""Second proof of capability (scratch copy of the c1b oracle): a real sprite of ANOTHER map as the target (KINDS 'map69-sprite126': the hammer-only post, balance record 382, answers only nibble 2)
and the rebound of the flail with and without a hit. Reads the repository only; writes nothing."""
import os, sys
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE + "/oracle")
import kworld as K
import world as W

KIT = (1, 17, 25)
CASES = [("basic sword", KIT, 1), ("Steel Edge", KIT + (2,), 1), ("Chain Throw", KIT + (9,), 3), ("Crusher", KIT + (10,), 3)]


def run(name, owned, weapon, target_x, kind, ticks=36):
    inv = dict(name=name, owned=owned, weapon=weapon, item=None, desc=name)
    m, meta = K.setup_inv(inv, hero_px=(212, 96), targets=[(target_x, 11, 0)], kind=kind)
    pad = W.Pad()
    rows = []
    for t in range(1, ticks + 1):
        m.tick_no = t
        K.tick(m, pad, 0x80 if t == 1 else 0)
        h = K.read_entity(m, 0)
        tt = K.read_entity(m, 1)
        rows.append((t, h["anim"], h["hit_counter"], tt["touching_slot"], tt["c1d0"], tt["hp"], h["pos"][0] >> 16, tt["anim"]))
    return rows


for name, owned, weapon in CASES:
    for kind, tx, label in ((None, 20, "Beannoid dx=40"), (None, 30, "Beannoid dx=100 (out of reach)"), ("map69-sprite126", 20, "hammer post dx=40")):
        try:
            rows = run(name, owned, weapon, tx, kind)
        except Exception as ex:
            print("%-12s %-30s ERROR %r" % (name, label, ex))
            continue
        touch = [r[0] for r in rows if r[3] == 0]
        hits = [r[0] for r in rows if r[4] == 0x19]
        anims = []
        for r in rows:
            if not anims or anims[-1][1] != r[1]:
                anims.append((r[0], hex(r[1] & 0xFFFF)))
        print("%-12s %-30s touch ticks=%s  (+0x1D0==0x19 at %s) hero anims=%s hero x end=%s target hp end=%s" % (name, label, touch[:3], hits[:3], anims, rows[-1][6], rows[-1][5]))
