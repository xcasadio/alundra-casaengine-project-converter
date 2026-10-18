import sys, os
BIN = os.path.abspath('../bin'); sys.path.insert(0, BIN)
import fw, world as W, c1world as C
SQ = W.SQUARE
def D_addr(m, slot):
    e = W.ENT0 + W.STRIDE * slot
    code = m.peek(e + 0x70 + 4 * 3)
    p = m.peek(C.HANDLER_TABLES + 4 * 3)
    return code, m.peek(p + 4 * code)
def kill_on_D(m):
    code, addr = D_addr(m, 1)
    print('target D code', code, 'handler address %08X' % addr)
    def hook(m2):
        a0 = m2.r[4]
        slot = (a0 - W.ENT0) // W.STRIDE
        m2.journal.append((m2.tick_no, 'handler', [slot, 3, addr]))
        m2.poke(a0 + 0x10, 4)           # DestroyEntity-like: status 4, nothing else touched (flags, body kept as the real one does)
        m2.r[2] = 0
    m.hooks[addr] = hook
def run(kill):
    C.map14().record['Header']['Ubuff'][13] = 32
    spec = dict(item=5, n=60, holds={1: SQ}, after=0, hero_px=(100, 100), direction=24, cells=None, targets=[(24, 12, 0)], init=[kill_on_D] if kill else [])
    res = fw.run(spec)
    C.map14().record['Header']['Ubuff'][13] = 8
    return res
for kill in (False, True):
    res = run(kill)
    S = None; ev = []
    for r in res['rows']:
        a = r['slots'].get(2)
        if a is not None and S is None: S = r['tick']
        t = r['slots'].get(1)
        if a is not None: ev.append((r['tick'] - S, a['status'], a['hitc'], t['status'] if t else None))
    hit = [e for e in ev if e[2]]
    print('kill target in its D handler:', kill, '| hit tick(S+)', hit[0][0] if hit else None, '| arrow status series around hit:', [e for e in ev if hit and hit[0][0] - 1 <= e[0] <= hit[0][0] + 3])
    rows = res['rows']
