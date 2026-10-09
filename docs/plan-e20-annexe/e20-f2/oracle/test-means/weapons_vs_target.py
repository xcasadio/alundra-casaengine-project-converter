"""Proof of capability (scratch copy of the c1b oracle): every weapon of the table swings against a real Beannoid (map 14, record 335) with the REAL hit pass.
Square at tick 1. Reports, per weapon: the hero animation, the first tick where the target is touched by the hero, the hero's CurrentAttack (attribute, power), the spawn calls.
Reads the repository only; writes nothing."""
import os, sys
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE + "/oracle")
import kworld as K
import world as W

KIT = (1, 17, 25)
WEAPONS = [
    # name, owned items, SetPlayerWeaponId argument
    ("basic sword", KIT, 1),
    ("Steel Edge", KIT + (2,), 1),
    ("Demon Buster", KIT + (3,), 1),
    ("Holy Sword", KIT + (4,), 1),
    ("item 8", KIT + (8,), 1),
    ("Chain Throw (flail)", KIT + (9,), 3),
    ("Crusher", KIT + (10,), 3),
    ("Hunter's Bow", KIT + (5,), 2),
    ("William's Bow", KIT + (6,), 2),
    ("Ice Rod", KIT + (14,), 4),
    ("Fire Rod", KIT + (16,), 5),
    ("Magic staff", KIT + (7,), 6),
]


def run(name, owned, weapon, dx, ticks=40):
    inv = dict(name=name, owned=owned, weapon=weapon, item=None, desc=name)
    m, meta = K.make_world(inv, hero_dx=dx)
    pad = W.Pad()
    first_touch = None
    hero_anims = []
    attack_at_hit = None
    spawns = []
    for t in range(1, ticks + 1):
        m.tick_no = t
        K.tick(m, pad, 0x80 if t == 1 else 0)
        h = K.read_entity(m, 0)
        tt = K.read_entity(m, K.SLOT["T"])
        if not hero_anims or hero_anims[-1][1] != h["anim"]:
            hero_anims.append((t, h["anim"]))
        if first_touch is None and tt["touching_slot"] == 0:
            first_touch = t
            attack_at_hit = (h["attr"], h["power"], h["anim"])
    for entry in m.journal:
        if len(entry) >= 2 and "spawn" in str(entry[1]):
            spawns.append(entry)
    return first_touch, attack_at_hit, hero_anims, spawns, K.hero_stats(m)


for name, owned, weapon in WEAPONS:
    for dx in (40,):
        try:
            ft, atk, anims, spawns, stats = run(name, owned, weapon, dx)
        except Exception as ex:  # report, never hide
            print("%-22s dx=%d ERROR %r" % (name, dx, ex))
            continue
        print("%-22s dx=%d first touch tick=%s attack(attr,power,anim)=%s anims=%s spawns=%s stats=%s" % (
            name, dx, ft, atk, [(t, hex(a & 0xFFFF)) for t, a in anims[:6]], [(s[0], s[2]) for s in spawns][:3], stats))
