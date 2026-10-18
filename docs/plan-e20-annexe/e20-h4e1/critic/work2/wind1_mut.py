"""wind1_mut.py - h4e1 discovery: mutants of my model of the wind-1 driver (wind1_model.py), run on the SAME random states as the vector file (wind1_vec.py N SEED); the unmutated model equals the REAL MIPS code
on every one of them (wind1_vec.py: 0 mismatch), so a mutant that differs from the unmutated model on a vector is killed by that vector.
usage: python -B wind1_mut.py N SEED"""
import random
import sys
import os
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import wind1_model as WM
import wind1_vec as WV

N, SEED = int(sys.argv[1]), int(sys.argv[2])
rnd = random.Random(SEED)
states = [WV.rnd_state(rnd) for _ in range(N)]


def obs(ret, w):
    return (ret, w['phase'], w['item'], w['last'], w['rng'], w['hero']['anim'], tuple(tuple(s['args']) for s in w['spawns']), tuple(w['destroys']), tuple(w['force_z']), repr(w['query']))


base = [obs(*WM.model(s)) for s in states]
MUTS = """q_trunc q_round q_pos64 gate_off guard_accepts_34 guard_no_slope guard_no_cancel z_limit_60 z_gate_off z_ge last_ignored last_not_set last_not_cleared_random phase0_keeps_last near_le near_unsigned far_continues
target_z_64 target_xy_swapped dir_1 forcez_4 ground_check_in_target last_only_if_spawned one_draw draw_order_swapped scale_swapped offset_wrong rand_z_64 ground_check_off fire_every_tick end_one_call_late
end_no_phase phase2_guard phase2_inverted q_inclusive q_sort2 q_sort_stable q_mask2 q_type10 q_status_2_only q_status_1_to_3 q_no_c1d0 q_no_c1cc q_no_b4 q_no_fiche q_resp_c0 pivot_signed swap_le""".split()
tot_killed = 0
for mu in MUTS:
    kill = 0
    first = None
    for i, s in enumerate(states):
        try:
            o = obs(*WM.model(s, frozenset([mu])))
        except Exception as ex:                                      # a mutant that crashes the model is killed too
            o = ('EXC', repr(ex))
        if o != base[i]:
            kill += 1
            first = first if first is not None else i
    print('%-26s killed by %4d of %d vectors%s' % (mu, kill, N, '' if kill else '   <== SURVIVES'))
    tot_killed += 1 if kill else 0
print('mutants %d, killed %d, survivors %d' % (len(MUTS), tot_killed, len(MUTS) - tot_killed))
