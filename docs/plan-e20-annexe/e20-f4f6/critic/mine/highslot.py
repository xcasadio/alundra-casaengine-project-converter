import sys, os
BIN = os.path.abspath('../bin'); sys.path.insert(0, BIN)
import fw, world as W, c1world as C, mipsdis
SQ = W.SQUARE
def free_slot1(m):
    base = W.ENT0 + W.STRIDE * 1
    for o in range(0, W.STRIDE, 4):
        m.poke(base + o, 0)
    m.poke(base, 1)       # index word of the template block
def run(init=None):
    C.map14().record['Header']['Ubuff'][13] = 32
    spec = dict(item=5, n=75, holds={1: SQ}, after=0, hero_px=(100, 100), direction=24, cells=None, targets=[(24, 6, 0), (24, 12, 0)], init=[free_slot1] + (init or []))
    res = fw.run(spec)
    C.map14().record['Header']['Ubuff'][13] = 8
    return res
def orig(addr):
    o = mipsdis.off(addr); return int.from_bytes(mipsdis._data[o:o+4], 'little')
def summary(res):
    S = None; out = []
    for r in res['rows']:
        for s, d in r['slots'].items():
            if s and d['flags6C'] in (0x12074, 0x12144):
                if S is None: S = r['tick']; slot = s
                if s != slot: continue
                out.append((r['tick'] - S, d['status'], d['hitc']))
    hit = [x for x in out if x[2]]
    dead = [x for x in out if x[1] == 4]
    return ('arrow slot', slot, 'hit at S+', hit[0][0] if hit else None, 'status4 at S+', dead[0][0] if dead else None)
print('control      ', summary(run()))
# mutant: loop bound  slt v0,t3,a3  (0x8007F5F0) -> slt v0,t3,a3 with  a3+1  i.e. exclusive:  change to  'slt v0, a3, t3' is a different test; use: addiu t3,t3,-1 at 0x8007F4E4 (move t3,v0) -> count-1
w = (orig(0x8007F4E4) & 0xFFFF0000) | 0       # 'move t3, v0' is addu t3,v0,zero (R-type); replace by addiu t3,v0,-1
def patch(m):
    m.poke(0x8007F4E4, 0x2443FFFF if False else (0x24400000 | 0))   # placeholder, replaced below

def pm(m):
    m.poke(0x8007F4E4, 0x244BFFFF)     # move t3,v0  ->  addiu t3,v0,-1  : the scan stops one slot early
print('mutant bound ', summary(run(init=[pm])))
