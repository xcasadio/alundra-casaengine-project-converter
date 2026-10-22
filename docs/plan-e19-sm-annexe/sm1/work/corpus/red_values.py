"""For every sm1 site (0x60 0x61 0x3C 0x3D) of the corpus: the Result writers of the DLL (today: the four opcodes are skipped by size, Result untouched)
that reach the site along the control flow of its program, and the first Result reader that follows it.
Reaching-definitions over the CFG of the program (entry = the program's start code index, state Result = ENTRY for a fresh state).
Usage: python -B red_values.py [outfile.tsv]      (reads the repository + the export; writes only the outfile)
INFERENCE: control flow is decoded statically (both branches of every conditional); 0x78/0x7D calls are followed as jump + fall-through."""
import json, re, sys, os
REPO = "D:/development/repo/alundra-casaengine-project-converter"
BS = chr(92)

# DLL Result writers (result_writers.txt, HEAD): Dispatch cases that assign state.Result directly or through CheckFlagsOn/Off, DestroyMatchingEntities, CheckEntityInAabb.
WRITERS = {0x07, 0x70, 0x71, 0x87, 0x95, 0x6F, 0x2E, 0x33, 0x34, 0x44, 0xA8, 0xBA, 0x8C, 0x82, 0x83, 0x84, 0x99, 0x9A, 0x9F, 0xB1, 0x3B, 0x2F, 0x52, 0x2C, 0x3E, 0x3F, 0x43, 0x6E, 0xAD, 0xB8, 0x8D}
READERS = {0x03, 0x04, 0x4A, 0x4B}
SITE_OPS = {0x60, 0x61, 0x3C, 0x3D}


def sizes():
    s = {}
    for line in open(REPO + "/Alundra/Events/EventOpcodeSizeTable.cs", encoding="utf-8-sig"):
        m = re.search(r"\{\s*0x([0-9A-Fa-f]{2}),\s*new\((\d+),\s*\"([^\"]*)\"", line)
        if m:
            s[int(m.group(1), 16)] = (int(m.group(2)), m.group(3))
    return s


SIZES = sizes()


def events(mapno):
    idx = json.load(open(REPO + '/alundra-project/Maps/world-index.json', encoding='utf-8'))
    rel = idx[str(mapno)].replace(BS, '/')
    folder = os.path.dirname(REPO + '/alundra-project/' + rel)
    name = os.path.splitext(os.path.basename(rel))[0]
    return json.load(open(folder + '/events/' + name + '.events.json', encoding='utf-8'))


def s16(lo, hi):
    v = lo | (hi << 8)
    return v - 0x10000 if v & 0x8000 else v


def successors(codes, pc, entry):
    op = codes[pc]
    sz = SIZES.get(op, (1, '?'))[0] or 1
    nxt = pc + sz
    if op == 0xFF:
        return []
    if op == 0x02:
        return [pc + s16(codes[pc + 1], codes[pc + 2])]
    if op in (0x03, 0x04):
        return [nxt, pc + s16(codes[pc + 1], codes[pc + 2])]
    if op in (0x30, 0x31):
        return [nxt, pc + s16(codes[pc + 3], codes[pc + 4])]
    if op == 0x74:
        return [nxt, pc + s16(codes[pc + 1], codes[pc + 2])]
    if op == 0x49:
        return [entry]
    if op in (0x4A, 0x4B):
        return [nxt, entry]
    if op == 0x78:
        return [nxt, pc + s16(codes[pc + 1], codes[pc + 2])]
    if op == 0x7D:
        return []
    if op == 0x53:
        return [nxt]
    return [nxt]


def analyse(mapno, slot, prog):
    d = events(mapno)
    codes = d['Codes']
    entry = d['EventCodes%sTable' % slot][prog]
    # reaching definitions: IN[pc] = set of writers (pc of writer, or -1 for ENTRY)
    inn = {entry: {-1}}
    work = [entry]
    seen_pcs = set()
    while work:
        pc = work.pop()
        seen_pcs.add(pc)
        op = codes[pc]
        cur = set(inn[pc])
        if op in WRITERS:
            cur = {pc}
        for s in successors(codes, pc, entry):
            if s < 0 or s >= len(codes):
                continue
            old = inn.get(s)
            if old is None:
                inn[s] = set(cur)
                work.append(s)
            elif not cur <= old:
                old |= cur
                work.append(s)
    return codes, entry, inn


def main():
    out = open(sys.argv[1], 'w', encoding='utf-8') if len(sys.argv) > 1 else sys.stdout
    rows = [l.rstrip("\n").split("\t") for l in open(REPO + "/docs/plan-e19-sm-annexe/skipped-sites.tsv", encoding="utf-8")][1:]
    todo = {}
    for r in rows:
        mapno, slot, prog, pc, op = int(r[0]), r[2], int(r[3]), int(r[4]), int(r[5], 16)
        if op in SITE_OPS:
            todo.setdefault((mapno, slot, prog), []).append((pc, op, r[6]))
    print('map\tprog\tpc\top\toperands\treaching_result_writers_today\tfirst_reader_after', file=out)
    for key in sorted(todo):
        mapno, slot, prog = key
        codes, entry, inn = analyse(mapno, slot, prog)
        for pc, op, operands in sorted(todo[key]):
            ws = sorted(inn.get(pc, set()))
            wtxt = ','.join('ENTRY' if w == -1 else '@%d:%02X' % (w, codes[w]) for w in ws)
            sz = SIZES[op][0]
            nxt = pc + sz
            rd = '@%d:%02X' % (nxt, codes[nxt]) if codes[nxt] in READERS else 'other:@%d:%02X' % (nxt, codes[nxt])
            print('%d\t%s[%d]\t%d\t%02X\t%s\t%s\t%s' % (mapno, slot, prog, pc, op, operands, wtxt, rd), file=out)


main()
