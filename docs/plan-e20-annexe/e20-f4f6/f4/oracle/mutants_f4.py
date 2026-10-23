"""mutants_f4.py - E20.f4 T0: the one-word mutants of C94 (0x8007C0D8), E1 (0x8007ED10) and of the data the charged arrow reads, on the real machine (patched RAM, never simulated),
each compared with its control on the stated scenario key. A mutant is KILLED when the signature (the status, the position, the forces, the flags, the effects, the sounds and the draws of every tick of
the slots 1 and 2) differs from the control's. Usage (from a COPY of the annex, it writes ./out): python -B mutants_f4.py
The scenarios are those of ../../bin/run_charged.py (William's bow, item 6, release at tick 96, the arrow of type 7 born at S = 105).
"""
import os
import sys
import json

HERE = os.path.dirname(os.path.abspath(__file__))
BIN = os.path.join(HERE, '..', '..', 'bin')
sys.path.insert(0, BIN)
sys.path.insert(0, os.path.join(HERE, '..', '..', 'critic', 'mine'))
import fw
import world as W
import mipsdis

OUT = os.path.join(HERE, 'out')
os.makedirs(OUT, exist_ok=True)
SQ = W.SQUARE
HOLD95 = {t: SQ for t in range(1, 96)}
REC7 = 0x80160000 + 0x4000 * 2          # pworld.install_global: GREC + 0x4000 * (index of the type 7 among the sorted installed types 3, 4, 7, ...)
WALL = fw.wall_col(15)


def orig(addr):
    o = mipsdis.off(addr)
    return int.from_bytes(mipsdis._data[o:o + 4], 'little')


def patch(*pairs):
    def f(m):
        for a, w in pairs:
            m.poke(a, w & 0xFFFFFFFF)
    return f


def withlow(addr, low):
    return (addr, (orig(addr) & 0xFFFF0000) | (low & 0xFFFF))


def grass_cells(col, row):
    if 5 <= col <= 12 and row in (5, 6):
        return (0, 0, 0x02, 0)
    return (0, 0, 0, 0)


def sig(res, slots=(1, 2)):
    out = []
    for r in res['rows']:
        row = [r['tick'], r['draws'], tuple(r['effects']), tuple(j for j in r['journal'] if j.startswith('snd'))]
        for s in slots:
            d = r['slots'].get(s)
            row.append(None if d is None else (d['status'], d['px'], d['py'], d['pz'], d['fx'], d['fy'], d['fz'], d['adj_13C'], d['flags6C'], d['dir_t'], d['anim_t'], d['hitc'], d['c1d0'], d['atk_attr'], d['atk_pow'], d['bw'], d['bd'], d['bh'], d['hbz']))
        out.append(tuple(row))
    return out


def run(n, init=None, cells=None, targets=(), direction=24, after_tick=None):
    spec = dict(item=6, n=n, holds=dict(HOLD95), after=0, hero_px=(100, 100), direction=direction, cells=cells, targets=list(targets), init=init or [], after_tick=after_tick or [])
    return fw.run(spec)


def check_record(m):
    flags = m.peek(REC7 + 0x10) & 0xFFFF
    assert flags == 0x2054, hex(flags)           # the low half of the flags 0x32054 of the type 7 (the header byte 0x12 = 3 holds the rest)
    assert m.peek(REC7 + 0x1E) & 0xFF == 8       # BreakEffect 8, header byte 14


def poke_byte(m, a, v):
    m.poke8(a, v)


def z_mutant(m, t):
    if t == 96:
        ptr = m.peek(0x80127110)
        assert ptr, 'no weapon record pointer at the release'
        assert m.rb(ptr + 3) == 8, m.rb(ptr + 3)    # z byte of the fiche w9 of William charged: 8
        m.wb(ptr + 3, 16)


rows = []


def report(name, desc, scen, ctl, mut):
    killed = sig(ctl) != sig(mut)
    rows.append([name, desc, scen, 'KILLED' if killed else 'SURVIVED'])
    print('%-10s %-78s %-22s %s' % (name, desc, scen, 'KILLED' if killed else 'SURVIVED'))


def main():
    ctl_wall = run(160, cells=WALL, init=[check_record])
    ctl_grass = run(135, cells=grass_cells)
    ctl_north = run(135, direction=16)
    ctl_target = run(160, targets=[(24, 12, 0)], cells=WALL)
    M = [
        ('C94-1', 'trail period andi 3 -> 7 (0x8007C0F4)', 'wall east', ctl_wall, lambda: run(160, cells=WALL, init=[patch(withlow(0x8007C0F4, 7))])),
        ('C94-2', 'trail effect id 0x19 -> 0x18 (0x8007C128)', 'wall east', ctl_wall, lambda: run(160, cells=WALL, init=[patch(withlow(0x8007C128, 0x18))])),
        ('C94-3', 'trail velocity X sra 1 -> sra 0 (0x8007C144)', 'wall east', ctl_wall, lambda: run(160, cells=WALL, init=[patch((0x8007C144, orig(0x8007C144) & ~(0x1F << 6)))])),
        ('C94-3b', 'trail velocity Y sra 1 -> sra 0 (0x8007C158), hero facing north', 'free north', ctl_north, lambda: run(135, direction=16, init=[patch((0x8007C158, orig(0x8007C158) & ~(0x1F << 6)))])),
        ('C94-4', 'trail Z offset lui 8 -> 4 (0x8007C110)', 'wall east', ctl_wall, lambda: run(160, cells=WALL, init=[patch(withlow(0x8007C110, 4))])),
        ('C94-5', 'trail direction a2 = +0x98 -> 0 (0x8007C11C)', 'wall east', ctl_wall, lambda: run(160, cells=WALL, init=[patch((0x8007C11C, 0x00003021))])),
        ('C94-5b', 'trail direction a2 -> 0, hero facing NORTH (0x8007C11C)', 'free north', ctl_north, lambda: run(135, direction=16, init=[patch((0x8007C11C, 0x00003021))])),
        ('C94-6', 'CheckAndTriggerTileEffect call removed (0x8007C0E4 jal -> nop)', 'grass strip', ctl_grass, lambda: run(135, cells=grass_cells, init=[patch((0x8007C0E4, 0))])),
        ('E1-1', 'DestroyEntity fx -1 -> 0 (0x8007ED1C): no break effect', 'wall east', ctl_wall, lambda: run(160, cells=WALL, init=[patch(withlow(0x8007ED1C, 0))])),
        ('HDR-1', 'flag 0x20 set in the header of the type 7: the arrow no longer passes through (record +0x10)', 'vs target + wall', ctl_target,
         lambda: run(160, targets=[(24, 12, 0)], cells=WALL, init=[lambda m: m.poke16(REC7 + 0x10, m.peek(REC7 + 0x10) & 0xFFFF | 0x20)])),
        ('HDR-2', 'BreakEffect 8 -> 0 in the header (record +0x1E)', 'wall east', ctl_wall, lambda: run(160, cells=WALL, init=[lambda m: poke_byte(m, REC7 + 0x1E, 0)])),
        ('FICHE-1', 'z byte of the fiche of William charged 8 -> 16 (record pointer +3)', 'wall east', ctl_wall, lambda: run(160, cells=WALL, after_tick=[z_mutant])),
        ('SPAWN-1', '+1 of the creation Z removed (0x80039EA4 addiu 1 -> 0)', 'wall east', ctl_wall, lambda: run(160, cells=WALL, init=[patch(withlow(0x80039EA4, 0))])),
    ]
    for name, desc, scen, ctl, fn in M:
        report(name, desc, scen, ctl, fn())
    json.dump(rows, open(os.path.join(OUT, 'mutants_f4.json'), 'w'), indent=1)
    open(os.path.join(OUT, 'mutants_f4.txt'), 'w').write('\n'.join('%-10s %-78s %-22s %s' % tuple(r) for r in rows) + '\n')


if __name__ == '__main__':
    main()
