"""Dump the linear decode of every program that holds an sm1 site (0x60 0x61 0x3C 0x3D), marking the sites.
Usage: python -B dump_sites.py [outfile]    (reads the repository + the export; writes only the outfile)
The decode follows the size table of the HEAD from the entry of the program to the first 0xFF; a branch target is shown in the margin."""
import json, re, sys, glob, os
REPO = "D:/development/repo/alundra-casaengine-project-converter"
BS = chr(92)


def sizes():
    s = {}
    for line in open(REPO + "/Alundra/Events/EventOpcodeSizeTable.cs", encoding="utf-8-sig"):
        m = re.search(r"\{\s*0x([0-9A-Fa-f]{2}),\s*new\((\d+),\s*\"([^\"]*)\"", line)
        if m:
            s[int(m.group(1), 16)] = (int(m.group(2)), m.group(3))
    return s


def events_path(mapno):
    idx = json.load(open(REPO + '/alundra-project/Maps/world-index.json', encoding='utf-8'))
    rel = idx[str(mapno)].replace(BS, '/')
    folder = os.path.dirname(REPO + '/alundra-project/' + rel)
    name = os.path.splitext(os.path.basename(rel))[0]
    return folder + '/events/' + name + '.events.json'


SIZES = sizes()
SITE_OPS = {0x60, 0x61, 0x3C, 0x3D}


def main():
    out = open(sys.argv[1], 'w', encoding='utf-8') if len(sys.argv) > 1 else sys.stdout
    rows = [l.rstrip("\n").split("\t") for l in open(REPO + "/docs/plan-e19-sm-annexe/skipped-sites.tsv", encoding="utf-8")][1:]
    progs = {}
    for r in rows:
        mapno, name, slot, prog, pc, op = int(r[0]), r[1], r[2], int(r[3]), int(r[4]), int(r[5], 16)
        if op in SITE_OPS:
            progs.setdefault((mapno, slot, prog), set()).add(pc)
    for (mapno, slot, prog) in sorted(progs):
        d = json.load(open(events_path(mapno), encoding='utf-8'))
        codes = d['Codes']
        start = d['EventCodes%sTable' % slot][prog]
        print('=== map %d %s[%d] entry @%d  sites %s' % (mapno, slot, prog, start, sorted(progs[(mapno, slot, prog)])), file=out)
        pc = start
        n = 0
        while pc < len(codes) and n < 400:
            op = codes[pc]
            sz = SIZES.get(op, (1, '?'))[0] or 1
            mark = '*' if pc in progs[(mapno, slot, prog)] else ' '
            print('%s@%5d %02X %-24s [%s]' % (mark, pc, op, ' '.join('%02X' % b for b in codes[pc + 1:pc + sz]), SIZES.get(op, (0, '?'))[1]), file=out)
            if op == 0xFF:
                break
            pc += sz
            n += 1
        print(file=out)


main()
