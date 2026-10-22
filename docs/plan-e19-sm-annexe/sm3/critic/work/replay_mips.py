"""Replay of the preparation's 837 vectors on the critic's OWN MIPS interpreter (mymips.py) running the real handlers.
Usage: python -B replay_mips.py <vectors.jsonl>   (reads the preparation's data only; rebuilds 9E grids from 'cells'/'grid_seed' via GRIDFN)"""
import json, sys, collections
import mylib as m, mymips as mm

ENT0 = 0x80127D30; STRIDE = 0x294; NADDR = 0x80134360; BUF = 0x8013D8D4
STATE = 0x8013D9E8; CODE = 0x80150000; PCP = 0x80150100; MAPPTR = 0x800DD86C; MAPBASE = 0x80160000
TABLE = 0x80098FAC; SP = 0x801F0000
F = dict(parent=0x0C, status=0x10, platform=0x28, ref=0x48, flags=0x6C, tanim=0x88, anim=0xB4, forcez=0xB8, riding=0x12C, xcoll=0x130)

def eaddr(i): return ENT0 + STRIDE * i

def build(vec, gridfn=None):
    mem = mm.Mem()
    N = vec['n_last']
    for i, s in enumerate(vec['slots']):
        b = eaddr(i)
        def pw(off, val): mem.ww(b + off, val & mm.M32)
        pw(F['status'], s['status']); pw(F['ref'], s['ref_id'] & mm.M32); pw(F['flags'], s['flags'])
        pw(F['anim'], s['anim_flags']); pw(F['forcez'], s['force_z'] & mm.M32); pw(F['tanim'], s['target_anim'])
        for k in ('platform', 'riding', 'xcoll', 'parent'):
            v = s[k]; pw(F[k], 0 if v is None else eaddr(v))
    mem.ww(NADDR, N)
    ins = [int(x, 16) for x in vec['instr']] + [0xFF]
    for i, b in enumerate(ins): mem.wb(CODE + i, b)
    mem.ww(PCP, CODE)
    mem.ww(STATE + 0x2C, vec['result_before'])
    if vec['op'] == '0x9E':
        mem.ww(MAPPTR, MAPBASE)
        grid = gridfn(vec)
        for idx, word in enumerate(grid):
            a = MAPBASE + 0x604 + idx * 8
            mem.wh(a, word)
    mem.writes.clear()
    return mem

def run(vec, gridfn=None, trace=False):
    mem = build(vec, gridfn)
    stubs = {0x8003C914: lambda c: c.r.__setitem__(2, 1),
             0x8003C840: lambda c: (_ for _ in ()).throw(mm.Stop("fatal 8003C840"))}
    cpu = mm.CPU(mem, stubs)
    mem.trace_reads = trace
    op = int(vec['op'], 16)
    h = m.u32(TABLE + 4 * op)
    cpu.run(h, [eaddr(vec['ref']), 0, PCP, STATE], SP)
    res = mem.rw(STATE + 0x2C)
    return cpu.r[2], res, mem

if __name__ == '__main__':
    path = sys.argv[1]
    from gridlib import make_grid
    bad = collections.Counter(); n = 0; byop = collections.Counter()
    for line in open(path, encoding='utf-8'):
        vec = json.loads(line); n += 1
        v0, res, mem = run(vec, make_grid)
        ok = (v0 == vec['expect']['v0'] and res == vec['expect']['result'])
        # writes outside stack / buffer / state result
        other = [w for w in mem.writes if not (SP - 0x1000 <= w[0] < SP) and not (BUF <= w[0] < BUF + 4 * 70) and w[0] != STATE + 0x2C]
        if not ok or other:
            bad[vec['op']] += 1
            print("MISMATCH", vec['id'], "v0", v0, vec['expect']['v0'], "res", res, vec['expect']['result'], "other", other[:3])
        byop[vec['op']] += 1
    print("replayed", n, "vectors; mismatches", sum(bad.values()), dict(byop))
