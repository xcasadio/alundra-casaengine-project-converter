import sys
from xref import ins, jal_callers
idx = {a:i for i,(a,m,o) in enumerate(ins)}
def func_start(addr):
    i = idx[addr]
    while i>0:
        a,m,o = ins[i]
        if m=='addiu' and o.startswith('$sp, $sp, -'):
            return a
        i -= 1
for t in sys.argv[1:]:
    a=int(t,16); fs=func_start(a)
    print(t, 'function start', hex(fs), 'callers', [hex(x) for x in jal_callers(fs)])
