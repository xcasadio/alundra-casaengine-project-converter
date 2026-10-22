import sys, random
sys.path.insert(0,'.')
import oracle, emu
m = oracle.make_machine()
rng = random.Random(5)
w = oracle.World(rng)
w.write(m)
for i,x in enumerate([0x60, 0x81]+[0]*8): m.wb(oracle.CODE+i, x)
m.ww(oracle.PCP, oracle.CODE)
trace=[]
orig=m.step
def step():
    trace.append(m.pc)
    orig()
m.step=step
try:
    m.call(m.rw(oracle.TABLE+4*0x60), oracle.ent_addr(0), oracle.ent_addr(0), oracle.PCP, oracle.STATE)
except Exception as ex:
    print(ex)
    print([hex(x) for x in trace[-12:]])
