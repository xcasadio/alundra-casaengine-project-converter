import json, sys

d = json.load(open(sys.argv[1]))
births = d["births"]
rows = []
for k, v in births.items():
    tr = v["track"]
    # track entries: (t, st, id, type, x, y, z, tanim, canim, fz, hp, hitc, flags)
    first = tr[0]
    # first tick with status 3
    st3 = next((e[0] for e in tr if e[1] == 3), None)
    # first tick with tanim == 1
    ta1 = next((e[0] for e in tr if e[7] == 1), None)
    # tick when fz reaches 0 or z stops changing
    zs = [(e[0], e[6]) for e in tr]
    lastz = tr[-1][6]
    rows.append((v["birth"], k, v["last"], st3, ta1, first[4] >> 16, first[5] >> 16, first[6] >> 16, first[9], first[12], lastz >> 16, first[7], first[8]))
rows.sort()
print("birth key last status3 tanim1 | x y z(px) fz flags | lastz(px) tanim canim")
for r in rows:
    birth, k, last, st3, ta1, x, y, z, fz, fl, lz, ta, ca = r
    print("%3d %-8s last=%3d life=%2d st3@%s ta1@%s | pos=(%d,%d) z=%d fz=%d(0x%x) flags=0x%x | lastz=%d ta=%d ca=%d" % (birth, k, last, last - birth + 1, st3, ta1, x, y, z, fz, fz & 0xFFFFFFFF, fl & 0xFFFFFFFF, lz, ta, ca))
