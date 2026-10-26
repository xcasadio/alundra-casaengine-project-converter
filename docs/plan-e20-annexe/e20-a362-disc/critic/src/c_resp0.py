from scen_lib import *
m, STATS = mk(0x23)
print('at setup: sheet byte 0x8012700E =', hex(m.rb(0x8012700E)))
out = []
def hook(m, t, r):
    if t <= 4: out.append((t, hex(m.rb(0x8012700E))))
log = run(m, 5, lambda t: 0, on_tick=hook)
print(out)
