"""mutants_f5.py - E20.f5 T0: the one-word mutants of E17 (0x8007F420) and of the data the normal arrows read, on the real machine (patched RAM, never simulated), each compared with its control on the
stated scenario. A mutant is KILLED when the signature (the status, the position, the forces, the flags, the box, the effects, the sounds and the draws of every tick of the slots 1 to 3) differs from the
control's. 15 mutants: E17-1 to E17-12 (the 12 of the discovery critic, ../../critic/mine/mutants_crit.py, with the addresses and words it used), E17-13 (the bound of the scan), DELAY-1 (the delay of the
arc, the fiche w8) and ZBYTE-1 (the z byte of the fiche w8). E17-8 and E17-9 are killed by the injections after the touch (the keys arrow3_target_filter_b4 and arrow3_target_filter_class).
Usage (from a COPY of the annex, it writes ./out): python -B mutants_f5.py
"""
import os
import sys
import json

HERE = os.path.dirname(os.path.abspath(__file__))
BIN = os.path.join(HERE, '..', '..', 'bin')
sys.path.insert(0, BIN)
sys.path.insert(0, os.path.join(HERE, '..', '..', 'critic', 'mine'))
sys.path.insert(0, HERE)
import fw
import world as W
import c1world as C
import mipsdis
import run_f5 as R

OUT = os.path.join(HERE, 'out')
os.makedirs(OUT, exist_ok=True)
SQ = W.SQUARE
WALL = fw.wall_col(15)
ENT0, STRIDE = W.ENT0, W.STRIDE
NOP = 0


def orig(addr):
    o = mipsdis.off(addr)
    return int.from_bytes(mipsdis._data[o:o + 4], 'little')


def patch(*pairs):
    def f(m):
        for a, w in pairs:
            m.poke(a, w & 0xFFFFFFFF)
    return f


def withop(addr, opcode):                       # replace the 6-bit opcode of the word at addr
    return (addr, (orig(addr) & 0x03FFFFFF) | (opcode << 26))


def uncond(addr):                              # branch always taken, same offset
    return (addr, (4 << 26) | (orig(addr) & 0xFFFF))


def withlow(addr, low):                         # replace the low 16 bits (immediate) of the word at addr
    return (addr, (orig(addr) & 0xFFFF0000) | (low & 0xFFFF))


def sig(res, slots=(1, 2, 3)):
    out = []
    for r in res['rows']:
        row = [r['tick'], r['draws'], tuple(r['effects']), tuple(j for j in r['journal'] if j.startswith('snd'))]
        for s in slots:
            d = r['slots'].get(s)
            row.append(None if d is None else (d['status'], d['px'], d['py'], d['pz'], d['fx'], d['fy'], d['fz'], d['adj_13C'], d['flags6C'], d['dir_t'], d['anim_t'], d['hitc'], d['c1d0'],
                                              d['atk_attr'], d['atk_pow'], d['bw'], d['bd'], d['bh'], d['hbz'], d['reset_AC']))
        out.append(tuple(row))
    return out


def go(item, n, init=None, cells=None, targets=(), tall=None, kind=None, after_tick=None):
    """a run with the Beannoid body height patched when asked (restored after), the way run_arrows.scen does"""
    spec = R.target_spec(item, n, after_tick=after_tick, init=init, kind=kind, targets=targets, cells=cells)
    if tall is not None:
        C.map14().record['Header']['Ubuff'][13] = tall
    try:
        return fw.run(spec)
    finally:
        if tall is not None:
            C.map14().record['Header']['Ubuff'][13] = 8


def with_injection(off, fn):
    def after():
        return [R.inj(1, 2, off, fn, {})]
    return after


def poke_byte(addr, value):
    def f(m):
        m.poke8(addr, value)
    return f


def set_type_1ad(m):
    m.poke(ENT0 + STRIDE * 1 + 0x68, 0x1AD)    # the target now has the type of the "Interrupteur ballon" (map-local 173 -> +0x68 = 0x100 + 0xAD)


rows = []


def report(name, desc, scen, ctl, mut):
    killed = sig(ctl) != sig(mut)
    rows.append([name, desc, scen, 'KILLED' if killed else 'SURVIVED'])
    print('%-8s %-78s %-24s %s' % (name, desc, scen, 'KILLED' if killed else 'SURVIVED'))


def main():
    # the scenarios of the controls: each is run once unmutated, then once per mutant that it judges
    S = {}
    S['wall'] = lambda init=None: go(5, 75, init=init, cells=WALL)
    S['invuln'] = lambda init=None: go(5, 75, init=init, targets=[(24, 12, 0)], tall=32, after_tick=[R.inj(1, 2, 0x1CC, lambda v: 0x78, {})])
    S['filter_b4'] = lambda init=None: go(5, 75, init=init, targets=[(24, 12, 0)], tall=32, after_tick=[R.inj(1, 2, 0xB4, lambda v: v | 0x40, {})])
    S['filter_class'] = lambda init=None: go(5, 75, init=init, targets=[(24, 12, 0)], tall=32, after_tick=[R.inj(1, 2, 0x6C, lambda v: v & ~1, {})])
    S['1ad'] = lambda init=None: go(5, 75, init=[set_type_1ad] + (init or []), targets=[(24, 12, 0)], tall=32)
    S['higher_slot'] = lambda init=None: go(5, 75, init=[R.free_slot1] + (init or []), targets=[(24, 6, 0), (24, 12, 0)], tall=32)
    S['hit'] = lambda init=None: go(5, 75, init=init, targets=[(24, 12, 0)], tall=32)
    ctl = {k: f() for k, f in S.items()}
    M = [
        ('E17-1', 'HitCounter gate beqz -> bnez (0x8007F470)', 'wall', [patch(withop(0x8007F470, 5))]),
        ('E17-2', 'bounce direction +0x10 -> +0x08 (0x8007F628)', 'wall', [patch(withlow(0x8007F628, 0x08))]),
        ('E17-3', 'bounce gravity bit ori 0x140 -> 0x040 (0x8007F630)', 'wall', [patch(withlow(0x8007F630, 0x040))]),
        ('E17-4', 'bounce flag ori 0x140 -> 0x100 (0x8007F630): 0x40 is already set (equivalent?)', 'wall', [patch(withlow(0x8007F630, 0x100))]),
        ('E17-5', 'bounce mask andi -0x31 -> -0x01 (0x8007F638): a dead write, seen by flags6C only', 'wall', [patch(withlow(0x8007F638, 0xFFFF))]),
        ('E17-6', 'status 2 on not-found -> 3 (0x8007F600)', 'invuln', [patch((0x8007F600, 0x24020003))]),
        ('E17-7', 'skip rule +0x1CC != 0 removed (0x8007F510 bnez -> nop)', 'invuln', [patch((0x8007F510, NOP))]),
        ('E17-8', 'skip rule +0xB4 & 0x40 removed (0x8007F500 bnez -> nop)', 'filter_b4', [patch((0x8007F500, NOP))]),
        ('E17-9', 'skip rule (flags & mask) == 0 removed (0x8007F524 beqz -> nop)', 'filter_class', [patch((0x8007F524, NOP))]),
        ('E17-10', 'type 0x1AD -> 0x1AC (0x8007F5D4)', '1ad', [patch(withlow(0x8007F5D4, 0x1AC))]),
        ('E17-11', 'phase compare +0x88 == 1 -> == 2 (0x8007F434)', 'wall', [patch(withlow(0x8007F434, 2))]),
        ('E17-12', 'phase end +0xAC test -> always skip the destroy (0x8007F448 bne -> b)', 'wall', [patch(uncond(0x8007F448))]),
        ('E17-13', 'the scan stops one slot early: move t3,v0 -> addiu t3,v0,-1 (0x8007F4E4)', 'higher_slot', [patch((0x8007F4E4, 0x244BFFFF))]),
        ('DELAY-1', 'delay of the arc in the fiche w8, 8 -> 9 (0x80028CE3)', 'wall', [poke_byte(0x80028CE3, 9)]),
        ('ZBYTE-1', 'z byte of the fiche w8, 0x10 -> 0x11 (0x80028CE4)', 'wall', [poke_byte(0x80028CE4, 0x11)]),
    ]
    for name, desc, scen, init in M:
        report(name, desc, scen, ctl[scen], S[scen](init=init))
    json.dump(rows, open(os.path.join(OUT, 'mutants_f5.json'), 'w'), indent=1)
    open(os.path.join(OUT, 'mutants_f5.txt'), 'w').write('\n'.join('%-8s %-78s %-24s %s' % tuple(r) for r in rows) + '\n')


if __name__ == '__main__':
    main()
