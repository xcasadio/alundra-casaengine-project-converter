import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import corpus2 as C, cfg2, memo_static as M
progs, sites = M.programs_from_tsv()
site_set = {(m,s,p,pc) for m,s,p,pc,o in sites}
for (m,s,p) in progs:
    base,d = C.load(m)
    codes = d['Codes']; entry = d[C.TABLE_KEYS[s]][p]
    seen, marks, ru, extra = cfg2.reach_walkprogram(codes, entry)
    print('=== map %d %s[%d] entry=%d reach=%d instrs, marks=%s, retusers=%s' % (m,s,p,entry,len(seen),sorted(marks),sorted(ru)))
    for pc in sorted(seen):
        op,size,ops = C.decode_one(codes,pc)
        mark = '>>' if (m,s,p,pc) in site_set else '  '
        print(C.fmt(pc,op,ops,mark))
