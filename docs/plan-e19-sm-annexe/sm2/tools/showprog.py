import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import corpus2 as C, cfg2, memo_static as M
progs, sites = M.programs_from_tsv()
site_set = {(m,s,p,pc) for m,s,p,pc,o in sites}
for arg in sys.argv[1:]:
    m, s, p = arg.split(':')
    m = int(m); p = int(p)
    base,d = C.load(m)
    codes = d['Codes']; entry = d[C.TABLE_KEYS[s]][p]
    seen, marks, ru, extra = cfg2.reach_walkprogram(codes, entry)
    print('=== map %d %s[%d] entry=%d' % (m,s,p,entry))
    for pc in sorted(seen):
        op,size,ops = C.decode_one(codes,pc)
        print(C.fmt(pc,op,ops,'>>' if (m,s,p,pc) in site_set else '  '))
