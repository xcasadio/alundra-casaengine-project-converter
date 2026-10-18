"""c1_interrupt.py (h4c1 discovery): earth 1 interrupted by the hurt animation 0x31 poked at the start of tick T (the method of e20-h4 interrupt_check.py), pworld machine."""
import sys
import c1_pworld as C1
import world as W
P = W.ent(0)
T = int(sys.argv[1]) if len(sys.argv) > 1 else 60
def poke31(m, t):
    pass
res = None
import fw, pworld as PW
def ticker(m, pad, hold):
    if m.tick_no == T:
        m.poke(P + 0x88, 0x31)
    W.tick(m, pad, hold)
spec = dict(item=None, n=T + 80, holds={1: W.CIRCLE}, hero_px=(100, 100), direction=24, targets=[], seed=0x12345678,
            types=tuple(PW.PROJECTILE_TYPES) + (16,), keep=set(PW.REAL) | {0x8007EDA0}, init=[C1.init_tome(0x2B)], ticker=ticker)
res = fw.run(spec)
m = res['m']
last = None
for r in res['rows']:
    t = r['tick']
    ents = [(s, d['status'], d['type68'], d['anim_t']) for s, d in sorted(r['slots'].items()) if s != 0]
    item = None
    if t in (T - 1, T, T + 1, T + 2, T + 3) or t in (T + 20, T + 40, T + 79):
        print('tick', t, 'rocks', [(s, st, a) for s, st, ty, a in ents if ty == 16], 'journal', [j for j in r['journal'] if not j.startswith('stub')][:3])
births = []
prev = set()
for r in res['rows']:
    cur = {s for s, d in r['slots'].items() if d['type68'] == 16}
    new = cur - prev
    if new:
        births.append((r['tick'], sorted(new)))
    prev = cur
print('birth ticks (slot sets, may merge reuse):', [b[0] for b in births])
print('item in use [0x80127164] at end:', hex(m.peek(0x80127164)), 'hero anim', hex(m.peeks(P + 0x88)))
