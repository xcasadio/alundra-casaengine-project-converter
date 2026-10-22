"""Read-only: for each sm3 site, the last instruction of the program (decoded linearly from its entry to the site) that writes Result in today's DLL; none = the state's initial Result (0)."""
import csv, sys, os
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'tools'))
import corpus
WRITERS = {0x04, 0x07, 0x2C, 0x2F, 0x3B, 0x3E, 0x3F, 0x43, 0x44, 0x4B, 0x52, 0x6E, 0x6F, 0x70, 0x71, 0x7F, 0x82, 0x83, 0x84, 0x87, 0x8C, 0x8D, 0x95, 0x99, 0x9A, 0x9F, 0xA8, 0xAD, 0xB1, 0xB8, 0xBA}
# 0x04 and 0x4B only READ Result; keep them out of the writers
WRITERS -= {0x04, 0x4B}
S3 = {'A9', 'AA', 'B2', 'B4', 'B5', 'B6', 'B7', '9E'}
rows = [r for r in csv.DictReader(open(os.path.join(corpus.REPO, 'docs/plan-e19-sm-annexe/skipped-sites.tsv'), encoding='utf-8'), delimiter='\t') if r['opcode'] in S3]
for r in rows:
    mid, slot, prog, pc = int(r['map']), r['program'] and r['slot'], int(r['program']), int(r['pc'])
    base, d = corpus.load(mid)
    entry = d[corpus.TABLE_KEYS[slot]][prog]
    seq = corpus.decode(d['Codes'], entry, count=5000, stop_at_end=False)
    last = None
    jumps = []
    for p, o, ops in seq:
        if p >= pc:
            break
        if o in WRITERS:
            last = (o, p)
        if o in (0x02, 0x03, 0x04, 0x49, 0x4A, 0x4B, 0x74, 0x57, 0x58, 0x7D, 0x78):
            jumps.append((o, p))
    print('%3d %s[%2d] @%-5d %s %-6s last Result writer: %-14s control flow before: %s' % (mid, slot, prog, pc, r['opcode'], r['operands'], ('%02X@%d' % last) if last else 'none(initial 0)', ' '.join('%02X@%d' % j for j in jumps[:6])))
