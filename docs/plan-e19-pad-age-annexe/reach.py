import sys, re, collections
import lib
ins = lib.all_ins()
idx = {i.address:k for k,i in enumerate(ins)}
# function starts: addiu $sp,$sp,-N
starts = sorted(i.address for i in ins if i.mnemonic=='addiu' and i.op_str.startswith('$sp, $sp, -'))
import bisect
def func_of(a):
    k = bisect.bisect_right(starts, a)-1
    return starts[k] if k>=0 else None
def func_end(s):
    k = bisect.bisect_right(starts, s)
    return starts[k] if k < len(starts) else None
# pad reads
pads = {}
for line in open('padrefs.txt'):
    a = int(line.split()[0],16); 
    if 'addiu' in line: continue
    pads[a] = line.split('->')[1].strip()
def calls(f):
    e = func_end(f)
    out = []
    a = f
    while e is None or a < e:
        k = idx.get(a)
        if k is None: break
        i = ins[k]
        if i.mnemonic in ('jal',):
            try: out.append((a, int(i.op_str,16)))
            except: pass
        a += 4
        if e is None and a > f+0x3000: break
    return out
def reads(f):
    e = func_end(f)
    return [(a,w) for a,w in pads.items() if f <= a and (e is None or a < e)]
def reach(root, depth=6):
    seen = {}
    q = collections.deque([(root,0)])
    while q:
        f,d = q.popleft()
        if f in seen: continue
        seen[f] = d
        if d >= depth: continue
        for a,t in calls(f):
            # ignore libc/bios range 0x80080000+
            if 0x80080000 <= t: continue
            q.append((t,d+1))
    return seen
if __name__ == '__main__':
    roots = {k:int(v,16) for k,v in (x.split('=') for x in sys.argv[1:])}
    for name,r in roots.items():
        s = reach(r)
        print('==', name, hex(r), 'functions reached:', len(s))
        for f,d in sorted(s.items(), key=lambda x:x[1]):
            rd = reads(f)
            if rd:
                print('  d%d func %08x reads: %s' % (d, f, ', '.join('%08x:%s' % (a,w) for a,w in sorted(rd))))
