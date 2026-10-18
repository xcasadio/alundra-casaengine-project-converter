"""Critic: one-word mutants of the handlers E17, C94, C60, E9 on the real machine (patched RAM, never simulated), each compared with its control on a stated scenario key.
Shows which T0 mutants are killable by which scenario (and which are equivalent).  python -B mutants_crit.py   (from critic/mine; reads the scratch copy of the machine in ../bin/src)"""
import sys, os, json
BIN = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'bin'))
sys.path.insert(0, BIN)
import fw, world as W, c1world as C
import mipsdis                     # disassembler helper in this folder: only used to read the original words
SQ = W.SQUARE

def orig(addr):
    o = mipsdis.off(addr)
    return int.from_bytes(mipsdis._data[o:o + 4], 'little')

def patch(*pairs):
    def f(m):
        for a, w in pairs:
            m.poke(a, w & 0xFFFFFFFF)
    return f

def sig(res, slots=(1, 2)):
    out = []
    for r in res['rows']:
        row = [r['tick'], r['draws'], tuple(r['effects']), tuple(j for j in r['journal'] if j.startswith('snd'))]
        for s in slots:
            d = r['slots'].get(s)
            row.append(None if d is None else (d['status'], d['px'], d['py'], d['pz'], d['fx'], d['fz'], d['adj_13C'], d['flags6C'], d['dir_t'], d['anim_t'], d['hitc'], d['c1d0']))
        out.append(tuple(row))
    return out

def run(item, n, init=None, cells=None, targets=(), tall=None, direction=24, hero_px=(100, 100)):
    if tall is not None:
        C.map14().record['Header']['Ubuff'][13] = tall
    spec = dict(item=item, n=n, holds={1: SQ}, after=0, hero_px=hero_px, direction=direction, cells=cells, targets=list(targets), init=init or [])
    res = fw.run(spec)
    if tall is not None:
        C.map14().record['Header']['Ubuff'][13] = 8
    return res

def with_target_invuln(slot_t=1, slot_a=2):
    st = {}
    def f(m, t):
        a = W.ENT0 + W.STRIDE * slot_a
        if m.peek(a + 0x220) and not st.get('done'):
            m.poke(W.ENT0 + W.STRIDE * slot_t + 0x1CC, 0x78)
            st['done'] = t
    return f

def run_inv(item, n, init=None):
    C.map14().record['Header']['Ubuff'][13] = 32
    spec = dict(item=item, n=n, holds={1: SQ}, after=0, hero_px=(100, 100), direction=24, cells=None, targets=[(24, 12, 0)], init=init or [], after_tick=[with_target_invuln()])
    res = fw.run(spec)
    C.map14().record['Header']['Ubuff'][13] = 8
    return res

def run_1ad(item, n, init=None):
    C.map14().record['Header']['Ubuff'][13] = 32
    def i1(m):
        m.poke(W.ENT0 + W.STRIDE * 1 + 0x68, 0x1AD)
    spec = dict(item=item, n=n, holds={1: SQ}, after=0, hero_px=(100, 100), direction=24, cells=None, targets=[(24, 12, 0)], init=[i1] + (init or []))
    res = fw.run(spec)
    C.map14().record['Header']['Ubuff'][13] = 8
    return res

wall15 = fw.wall_col(15)
wall6 = fw.wall_col(6)
rows = []

def report(name, desc, scen, ctl, mut):
    killed = sig(ctl) != sig(mut)
    rows.append((name, desc, scen, 'KILLED' if killed else 'SURVIVED'))
    print('%-8s %-62s %-26s %s' % (name, desc, scen, 'KILLED' if killed else 'SURVIVED'))

def W_(addr, new):
    return (addr, new)

def withop(addr, opcode):                       # replace the 6-bit opcode of the word at addr
    return (addr, (orig(addr) & 0x03FFFFFF) | (opcode << 26))

def uncond(addr):                              # branch always taken, same offset
    return (addr, (4 << 26) | (orig(addr) & 0xFFFF))

def withlow(addr, low):                         # replace the low 16 bits (immediate) of the word at addr
    return (addr, (orig(addr) & 0xFFFF0000) | (low & 0xFFFF))

NOP = 0
# ---- E17 (arrow 3) -------------------------------------------------------------------------------------------------------------------
ctl_wall = run(5, 75, cells=wall15)
ctl_hit = run(5, 75, targets=[(24, 12, 0)], tall=32)
ctl_inv = run_inv(5, 75)
ctl_1ad = run_1ad(5, 75)
M = [
    ('E17-1', 'HitCounter gate beqz->bnez (0x8007F470)', withop(0x8007F470, 5)),
    ('E17-2', 'bounce direction +0x10 -> +0x08 (0x8007F628)', withlow(0x8007F628, 0x08)),
    ('E17-3', 'bounce gravity bit ori 0x140 -> 0x040 (0x8007F630)', withlow(0x8007F630, 0x040)),
    ('E17-4', 'bounce flag ori 0x140 -> 0x100 (0x8007F630) [dead edit?]', withlow(0x8007F630, 0x100)),
    ('E17-5', 'bounce mask andi -0x31 -> -0x01 (0x8007F638) [dead edit?]', withlow(0x8007F638, 0xFFFF)),
    ('E17-6', 'status 2 on not-found -> 3 (0x8007F608 addiu? uses $v0=2)', (0x8007F600, 0x24020003)),
    ('E17-7', 'skip rule +0x1CC != 0 removed (0x8007F510 bnez->nop)', (0x8007F510, NOP)),
    ('E17-8', 'skip rule +0xB4 & 0x40 removed (0x8007F500 bnez->nop)', (0x8007F500, NOP)),
    ('E17-9', 'skip rule (flags & mask)==0 removed (0x8007F524 beqz->nop)', (0x8007F524, NOP)),
    ('E17-10', 'type 0x1AD -> 0x1AC (0x8007F5D4)', withlow(0x8007F5D4, 0x1AC)),
    ('E17-11', 'phase compare +0x88 == 1 -> == 2 (0x8007F434)', withlow(0x8007F434, 2)),
    ('E17-12', 'phase end +0xAC test -> always skip destroy (0x8007F448 bne->b)', uncond(0x8007F448)),
]
scen = {'E17-1': ('wall', ctl_wall, lambda init: run(5, 75, init=init, cells=wall15)),
        'E17-2': ('wall', ctl_wall, lambda init: run(5, 75, init=init, cells=wall15)),
        'E17-3': ('wall', ctl_wall, lambda init: run(5, 75, init=init, cells=wall15)),
        'E17-4': ('wall', ctl_wall, lambda init: run(5, 75, init=init, cells=wall15)),
        'E17-5': ('wall', ctl_wall, lambda init: run(5, 75, init=init, cells=wall15)),
        'E17-6': ('invuln', ctl_inv, lambda init: run_inv(5, 75, init=init)),
        'E17-7': ('invuln', ctl_inv, lambda init: run_inv(5, 75, init=init)),
        'E17-8': ('hit', ctl_hit, lambda init: run(5, 75, init=init, targets=[(24, 12, 0)], tall=32)),
        'E17-9': ('hit', ctl_hit, lambda init: run(5, 75, init=init, targets=[(24, 12, 0)], tall=32)),
        'E17-10': ('1ad', ctl_1ad, lambda init: run_1ad(5, 75, init=init)),
        'E17-11': ('wall', ctl_wall, lambda init: run(5, 75, init=init, cells=wall15)),
        'E17-12': ('wall', ctl_wall, lambda init: run(5, 75, init=init, cells=wall15))}
for name, desc, pr in M:
    s, ctl, fn = scen[name]
    report(name, desc, s, ctl, fn([patch(pr)]))

# ---- C94 / E1 (charged arrow 7: William bow item 6, release at 96) -----------------------------------------------------------------
def run_charged(init=None, cells=None, direction=24):
    spec = dict(item=6, n=160, holds={t: SQ for t in range(1, 96)}, after=0, hero_px=(100, 100), direction=direction, cells=cells, targets=[], init=init or [])
    return fw.run(spec)
grass = None
try:
    sys.path.insert(0, BIN)
    import run_charged as RC
    grass = RC.grass_cells if hasattr(RC, 'grass_cells') else None
except Exception as e:
    grass = None
ctl_c = run_charged(cells=wall15)
for name, desc, pr in [
    ('C94-1', 'trail period andi 3 -> 7 (0x8007C0F4)', withlow(0x8007C0F4, 7)),
    ('C94-2', 'trail effect id 0x19 -> 0x18 (0x8007C128)', withlow(0x8007C128, 0x18)),
    ('C94-3', 'trail velocity sra 1 -> sra 0 (0x8007C144)', (0x8007C144, orig(0x8007C144) & ~(0x1F << 6))),
    ('C94-4', 'trail z offset 0x80000 -> 0x40000 (0x8007C110)', withlow(0x8007C110, 4)),
    ('C94-5', 'trail direction a2 = +0x98 -> 0 (0x8007C11C)', (0x8007C11C, 0x00003021)),
    ('E1-1', 'DestroyEntity fx -1 -> 0 (0x8007ED1C): no break effect', withlow(0x8007ED1C, 0)),
]:
    mut = run_charged(init=[patch(pr)], cells=wall15)
    report(name, desc, 'charged wall (east)', ctl_c, mut)
ctl_cn = run_charged(direction=16, cells=None)
mut = run_charged(init=[patch(withlow(0x8007C11C, 0) if False else (0x8007C11C, 0x00003021))], direction=16, cells=None)
report('C94-5b', 'trail direction a2 -> 0, hero facing NORTH', 'charged north free', ctl_cn, mut)

# ---- C60 / E9 (fire rod: item 16) ---------------------------------------------------------------------------------------------------
ctl_f = run(16, 70)
ctl_i = run(14, 70)
ctl_wf = run(16, 70, cells=wall6)
def gp_cells(gp):
    def cells(col, row):
        return (0, 0, 0, gp) if (col == 6 or col == 7) else (0, 0, 0, 0)
    return cells
ctl_w2 = run(16, 40, cells=gp_cells(0x02))
ctl_w8 = run(16, 40, cells=gp_cells(0x08))
for name, desc, pr, sc, cl, fn in [
    ('C60-1', 'trail period andi 3 -> 7 (0x8007A8B8)', withlow(0x8007A8B8, 7), 'fire flat', ctl_f, lambda i: run(16, 70, init=i)),
    ('C60-2', 'ice trail effect id 0x12 -> 0x11 (0x8007A8D8)', withlow(0x8007A8D8, 0x11), 'ice flat', ctl_i, lambda i: run(14, 70, init=i)),
    ('C60-3', 'fire z offset 19 -> 18 (0x8007A8E4)', (0x8007A8E4, orig(0x8007A8E4) & ~(0x1F << 6) | (18 << 6)), 'fire flat', ctl_f, lambda i: run(16, 70, init=i)),
    ('C60-4', 'trail vz negu -> move (0x8007A93C)', (0x8007A93C, 0x00021021 | (0 << 0)), 'fire flat', ctl_f, lambda i: run(16, 70, init=i)),
    ('E9-1', 'puff count slti 5 -> 4 (0x8007F1F0)', withlow(0x8007F1F0, 4), 'fire flat', ctl_f, lambda i: run(16, 70, init=i)),
    ('E9-2', 'periodic andi 7 -> 3 (0x8007EFD0)', withlow(0x8007EFD0, 3), 'fire flat', ctl_f, lambda i: run(16, 70, init=i)),
    ('E9-3', 'branch-A puff z offset sll 19 -> 18 (0x8007F00C)', (0x8007F00C, orig(0x8007F00C) & ~(0x1F << 6) | (18 << 6)), 'fire flat', ctl_f, lambda i: run(16, 70, init=i)),
    ('E9-4', 'branch-B slope1 test removed (0x8007F0F8 beq->nop)', (0x8007F0F8, NOP), 'fire on slope 1', ctl_w2, lambda i: run(16, 40, init=i, cells=gp_cells(0x02))),
    ('E9-5a', 'branch-B slope4 test -> always destroy (0x8007F100 bne->nop)', (0x8007F100, NOP), 'fire flat', ctl_f, lambda i: run(16, 70, init=i)),
    ('E9-5b', 'branch-B slope4 test -> never destroy (0x8007F100 bne->b) [pick catches 4 first?]', uncond(0x8007F100), 'fire on slope 4', ctl_w8, lambda i: run(16, 40, init=i, cells=gp_cells(0x08))),
    ('E9-6', 'branch-B destroy fx 6 -> 5 (0x8007F110)', withlow(0x8007F110, 5), 'fire on slope 1', ctl_w2, lambda i: run(16, 40, init=i, cells=gp_cells(0x02))),
    ('E9-7', 'draw scale 0x60001 -> 0x50001 (vx, 0x8007F180)', withlow(0x8007F180, 5), 'fire flat', ctl_f, lambda i: run(16, 70, init=i)),
    ('E9-8', 'explosion end +0xAC test -> always skip destroy (0x8007EFB0 bne->b)', uncond(0x8007EFB0), 'fire flat', ctl_f, lambda i: run(16, 70, init=i)),
    ('E9-9', 'branch-B flags ori 0x40 -> 0x00 (0x8007F208) [dead edit?]', withlow(0x8007F208, 0), 'fire flat', ctl_f, lambda i: run(16, 70, init=i)),
    ('E9-10', 'branch-B +0x88=1 not written (0x8007F200 sw->nop)', (0x8007F200, NOP), 'fire flat', ctl_f, lambda i: run(16, 70, init=i)),
]:
    report(name, desc, sc, cl, fn([patch(pr)]))
json.dump(rows, open('mutants_crit.json', 'w'), indent=1)
