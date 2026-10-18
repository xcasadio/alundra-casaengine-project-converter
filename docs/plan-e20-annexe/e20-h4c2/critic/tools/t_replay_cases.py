"""Replays the 609 cases of h4c2/keys/driver_2c_cases.json on MY interpreter (myemu.py) through the wrapper 0x8003634C."""
import json, sys
from myemu import Emu, M32, s32
import t_driver as D
KEYS = r"C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/disc-h4el/h4c2/keys/driver_2c_cases.json"
cases = json.load(open(KEYS))
POOL = 0x80140000
bad = 0
for ci, c in enumerate(cases):
    i = c['inputs']
    e = Emu()
    hz = i['hero_z_raw']
    e.poke(D.HERO + 0x88, i['hero_anim']); e.poke(D.HERO + 0x8C, 24)
    e.poke(D.HERO + 0x114, i['hero_x']); e.poke(D.HERO + 0x118, i['hero_y']); e.poke(D.HERO + 0x11C, hz)
    e.poke(D.HERO + 0x18C, i['slope'])
    e.poke(D.ITEM, 0x2C); e.poke(D.CNT, i['counter']); e.poke(D.PHASE, i['phase']); e.poke(D.SEED, i['seed'])
    top = 0
    for (slot, status, typ) in i['others']:
        a = D.ent(slot)
        e.poke(a + 0x10, status); e.poke(a + 0x68, typ); e.poke(a + 0x20, 5); e.poke(a + 0x88, 0)
        top = max(top, slot)
    e.poke(D.COUNT, top)
    tval = {'lt': (hz + 0x1000001) & M32, 'eq': (hz + 0x1000000) & M32, 'lo': 0}[i['terrain']]
    n = len(i['spawns'])
    for k in range(8):
        a = POOL + k * 0x294
        e.poke(a + 0x138, tval); e.poke(a + 0x10, 1)
    results = [POOL + k * 0x294 if s == 'ok' else 0 for k, s in enumerate(i['spawns'])]
    journal = []
    def spawn(m, results=results, journal=journal):
        sp = m.r[29]
        journal.append(['spawn', m.r[4], m.r[5], m.r[6], m.r[7], m.rw(sp + 0x10), m.rw(sp + 0x14), m.rw(sp + 0x18)])
        m.r[2] = results.pop(0) if results else 0
    def destroy(m):
        m.poke(m.r[4] + 0x10, 4); m.r[2] = 0
    e.hooks[0x80039F58] = spawn; e.hooks[0x8003A774] = destroy
    ret = s32(e.run(0x8003634C, ()))
    got = dict(ret=ret, journal=journal, rng_after=e.peek(D.SEED), phase_after=s32(e.peek(D.PHASE)), counter_after=s32(e.peek(D.CNT)),
               item_after=s32(e.peek(D.ITEM)), hero_anim_after=s32(e.peek(D.HERO + 0x88)),
               pool=[dict(status=s32(e.peek(POOL + k * 0x294 + 0x10)), force_z=e.peek(POOL + k * 0x294 + 0xB8)) for k in range(n)],
               others_after=[dict(slot=o[0], status=s32(e.peek(D.ent(o[0]) + 0x10)), b20=s32(e.peek(D.ent(o[0]) + 0x20)), anim=s32(e.peek(D.ent(o[0]) + 0x88))) for o in i['others']])
    exp = {k: c[k] for k in got}
    if got != exp:
        bad += 1
        if bad <= 5:
            print('DIFF case', ci, {k: (got[k], exp[k]) for k in got if got[k] != exp[k]})
print('cases', len(cases), 'diffs', bad)
