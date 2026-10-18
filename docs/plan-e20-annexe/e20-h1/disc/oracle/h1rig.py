"""h1rig.py - E20.h1 discovery (read-only on the repository and the binary): the shared rig of the vector generator, the replay and the coverage.

It runs the REAL code of ALUN_CD.EXE (France, SHA-1 a705f8da...) on the MIPS interpreter of the e20-h4b harness (a copy: h4b_h/), with the callees named in
STUBS replaced by journalled hooks:
  0x800490FC  PlaySoundEffect(id)                       1 register argument
  0x8003BDD8  CreateEffectEntity(0, id, anim, x, y, z)   4 register arguments + 2 stack arguments (y, z); returns the effect object (0 = pool full)
  0x8004E0F8  SetItemIdFromCurrentItemId()               no argument (dispatcher vectors only)
  0x80034224 0x80034320 0x8003453C 0x800348E0            the start functions of the OTHER tranches (beans, cape, bomb, tomes), 1 argument (dispatcher vectors only)
A stub returns, in order, the values listed in the vector's "stubs" entry; the arguments it saw are the vector's "calls" entry.
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, 'h4b_h'))
import vtrace as V
from machine import M32

HERO = 0x80127D30
STATS_PTR = 0x80176318      # [..] -> g_playerStats
INV_PTR = 0x8017631C        # [..] -> the inventory table, 4 bytes per item: +2 = count (s16)
INV_COUNT = 0x8017638C      # [..] = 0x63
IN_USE = 0x80127164
COUNTER = 0x8012715C
PHASE = 0x80127160
RNG = 0x80098708
STATS = 0x801E1000          # the harness's own address for the stats block (any RAM address works: the code reads it through the pointer)
INV = 0x801E2000
EFF0 = 0x801E8000           # effect objects: EFF0 + 0x100 * k
HERO2 = 0x801E4000          # a second "entity" for the vectors where a0 differs from the hero literal

# stub address -> (register arguments journalled, stack arguments journalled)
STUBS = {
    0x800490FC: (1, 0),
    0x8003BDD8: (4, 2),
    0x8004E0F8: (0, 0),
    0x80034224: (1, 0),
    0x80034320: (1, 0),
    0x8003453C: (1, 0),
    0x800348E0: (1, 0),
    0x80034680: (1, 0),
    0x800346F0: (1, 0),
    0x80034760: (1, 0),
    0x800347D4: (1, 0),
    0x80034870: (1, 0),
    0x8003499C: (0, 0),       # the start dispatcher, stubbed in the TryUseItem vectors
    0x8004DB90: (1, 0),       # the error printer UseItem calls for an index outside the inventory table
}


def raw_poke(mm, a, v):
    """a write the real code did not do: no entry in the access record, but the byte counts as initialised"""
    import emu as _emu
    mm._mark(a, 4)
    _emu.Emu.ww(mm, a, v & M32)


def install_stubs(m, stubs, calls, init_effect=False):
    """stubs: {addr: [returns...]} (consumed in order); calls: the list the hooks append ["0x....", [args]] to.
    init_effect (generation only): the effect object a stub hands out is initialised as InitEffect 0x8003BB14 leaves it: +0x3C/+0x40/+0x44 = x, y, z (the arguments), the forces +0x54/+0x58/+0x5C = 0.
    In a replay the object's bytes come from the vector's reads."""
    pending = {a: list(r) for a, r in stubs.items()}

    def make(addr):
        nreg, nstk = STUBS[addr]

        def hook(mm):
            sp = mm.r[29]
            args = [mm.r[4 + i] & M32 for i in range(nreg)]
            args += [mm.peek(sp + 0x10 + 4 * i) & M32 for i in range(nstk)] if nstk else []
            calls.append(["0x%08x" % addr, args])
            if not pending.get(addr):
                raise RuntimeError('stub %#x called more often than the vector lists' % addr)
            mm.r[2] = pending[addr].pop(0) & M32
            if init_effect and addr == 0x8003BDD8 and mm.r[2]:
                p = mm.r[2]
                raw_poke(mm, p + 0x3C, args[3])
                raw_poke(mm, p + 0x40, args[4])
                raw_poke(mm, p + 0x44, args[5])
                for off in (0x54, 0x58, 0x5C):
                    raw_poke(mm, p + off, 0)
        return hook

    for a in pending:                    # only the stubs the vector lists are installed: any other callee runs for real
        m.hooks[a] = make(a)
    return pending
