from scen_lib import *
m, STATS = mk(0x23)
rec = m.peek(HERO + 0x1C4)
m.poke8(rec + 6, 0x80)
seen = []
def hook(m, t, r):
    v = m.peek(HERO + 0x224)
    if v: seen.append((t, hex(v)))
log = run(m, 300, lambda t: W.CIRCLE if t == 1 else 0, on_tick=hook)
print('ticks where hero+0x224 (TouchingEntity) != 0 with the sheet record byte +6 = 0x80:', seen, '; final hp', log[-1]['hero']['hp'])
m2, _ = mk(0x23)
seen2 = []
def hook2(m, t, r):
    v = m.peek(HERO + 0x224)
    if v: seen2.append((t, hex(v)))
log2 = run(m2, 300, lambda t: W.CIRCLE if t == 1 else 0, on_tick=hook2)
print('plain:', seen2, 'final hp', log2[-1]['hero']['hp'])
