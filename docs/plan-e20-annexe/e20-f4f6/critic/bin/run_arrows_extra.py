"""run_arrows_extra.py - E17 branches that the plain scenarios do not reach (harness injections documented in each title)."""
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import fw
import world as W
import c1world as C
from run_arrows import scen, save, SQ, OUT

ENT0, STRIDE = W.ENT0, W.STRIDE


def hit_inject(target_slot, arrow_slot, value, state):
    """after the tick in which the arrow's hit counter (+0x220) is raised, write value into target +0x1CC (DamagedTickCounter).
    Models a D handler / script opcode 0x86 that makes the target invulnerable in the tick that follows the hit; in the binary the dispatch loop runs slots in
    ascending order, so a target of lower slot index runs its D handler BEFORE the arrow's E17 in that same events pass."""
    def f(m, t):
        a = ENT0 + STRIDE * arrow_slot
        if m.peek(a + 0x220) and not state.get('done'):
            m.poke(ENT0 + STRIDE * target_slot + 0x1CC, value)
            state['done'] = t
    return f


def run_invuln():
    C.map14().record['Header']['Ubuff'][13] = 32
    state = {}
    spec = dict(item=5, n=75, holds={1: SQ}, after=0, hero_px=(100, 100), direction=24, cells=None, targets=[(24, 12, 0)], after_tick=[hit_inject(1, 2, 0x78, state)])
    res = fw.run(spec)
    C.map14().record['Header']['Ubuff'][13] = 8
    save('arrow3_target_invulnerable_after_hit', res, [2, 1], rel=10,
         title='INJECTION: target +0x1CC := 0x78 right after the hit tick (stands for a D handler/opcode 0x86 that runs before E17): E17 finds no valid target, status back to 2, the arrow flies on (hit tick %s)' % state.get('done'))
    print('invuln hit tick', state.get('done'))


def run_1ad():
    C.map14().record['Header']['Ubuff'][13] = 32

    def init(m):
        m.poke(ENT0 + STRIDE * 1 + 0x68, 0x1AD)           # the target now has the type of the "Interrupteur ballon" (map-local 173 -> +0x68 = 0x100 + 0xAD = 0x1AD)
    spec = dict(item=5, n=75, holds={1: SQ}, after=0, hero_px=(100, 100), direction=24, cells=None, targets=[(24, 12, 0)], init=[init])
    res = fw.run(spec)
    C.map14().record['Header']['Ubuff'][13] = 8
    save('arrow3_target_1AD', res, [2, 1], rel=10, title='INJECTION: target +0x68 := 0x1AD (Interrupteur ballon): hit once, E17 excludes it, the arrow flies on to the end of the field')
    print('1ad done')


def run_two_targets():
    C.map14().record['Header']['Ubuff'][13] = 32
    spec = dict(item=5, n=60, holds={1: SQ}, after=0, hero_px=(100, 100), direction=24, cells=None, targets=[(24, 12, 0), (24, 11, 0)])
    res = fw.run(spec)
    C.map14().record['Header']['Ubuff'][13] = 8
    save('arrow3_two_targets', res, [3, 1, 2], rel=10, title='two targets side by side at x=300 (y=104 and y=96): one arrow hits both in the same tick, E17 destroys the arrow')
    print('two done')


if __name__ == '__main__':
    run_invuln()
    run_1ad()
    run_two_targets()
