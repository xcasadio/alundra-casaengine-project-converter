import csv, collections, sys
REPO = "D:/development/repo/alundra-casaengine-project-converter"
rows = list(csv.DictReader(open(REPO + "/docs/plan-e19-sm-annexe/skipped-sites.tsv", encoding="utf-8"), delimiter="\t"))
sm1 = {"60", "61", "3C", "3D"}
sm2 = {"6A", "6B", "6C", "6D"}
for name, ops in (("sm1", sm1), ("sm2", sm2)):
    sel = [r for r in rows if r["opcode"].upper() in ops]
    maps = sorted({int(r["map"]) for r in sel})
    print(name, len(sel), "sites", len(maps), "maps", maps)
    by_op = collections.Counter(r["opcode"].upper() for r in sel)
    for op in sorted(ops):
        mm = sorted({int(r["map"]) for r in sel if r["opcode"].upper() == op})
        print("  ", op, by_op.get(op, 0), "maps", mm)
    by_map = collections.Counter(int(r["map"]) for r in sel)
    print("   per map", dict(sorted(by_map.items())))
    slots = collections.Counter(r["slot"] for r in sel)
    print("   slots", dict(slots))
    ops1 = collections.Counter((r["opcode"].upper(), r["operands"]) for r in sel)
    print("   opcode+operand", dict(ops1))
    cons = collections.Counter((r["opcode"].upper(), r["next_opcode"], r["next_is_Result_consumer"]) for r in sel)
    print("   next", dict(cons))
    names = sorted({(int(r["map"]), r["map_name"]) for r in sel})
    print("   names", names)
