"""Critic-own extra mutants (not in the preparation's list) evaluated on the preparation's hand-written vectors with the critic's DLL-shape model.
A mutant is KILLED when some vector's expected Result differs from the mutant's Result."""
import json
import models as md

P = 'C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/sm-prep/sm3/vectors/sm3-vectors.jsonl'
vecs = [json.loads(line) for line in open(P, encoding='utf-8')]
hand = [v for v in vecs if not (v['id'].startswith('SWEEP') or v['id'].startswith('RAND'))]


def world(v):
    return dict(n_last=v['n_last'], slots=v['slots'], ref=v['ref'])


def instr(v):
    return [int(x, 16) for x in v['instr']]


def a9_by_refid(w, ref, ins):
    S = w['slots']
    g = md.dll_search(w, ref, ins[1])
    x = S[ref]['xcoll']
    return int(x is not None and any(S[i]['ref_id'] == S[x]['ref_id'] for i in g))


def aa_by_refid(w, ref, ins):
    S = w['slots']
    g = md.dll_search(w, ref, ins[1])
    return int(any(S[i]['xcoll'] is not None and S[S[i]['xcoll']]['ref_id'] == S[ref]['ref_id'] for i in g))


def a9_excl_player(w, ref, ins):
    S = w['slots']
    g = [i for i in md.dll_search(w, ref, ins[1]) if i != 0]
    x = S[ref]['xcoll']
    return int(any(x == i for i in g))


def aa_excl_player(w, ref, ins):
    S = w['slots']
    g = [i for i in md.dll_search(w, ref, ins[1]) if i != 0]
    return int(any(S[i]['xcoll'] == ref for i in g))


def b2_second_from_hero(w, ref, ins):
    S = w['slots']
    g1 = md.dll_search(w, ref, ins[1])
    if not g1:
        return 0
    g2 = md.dll_search(w, 0, ins[2])
    return int(any(a == b for a in g1 for b in g2))


def b2_first_from_hero(w, ref, ins):
    g1 = md.dll_search(w, 0, ins[1])
    if not g1:
        return 0
    g2 = md.dll_search(w, ref, ins[2])
    return int(any(a == b for a in g1 for b in g2))


def b7_status_ignored_member(w, ref, ins):
    # B7 that compares only the first element (first_only) is in the preparation's list; here: compares CurrentAnimationId (== 0 in a bare proxy)
    S = w['slots']
    g = md.dll_search(w, ref, ins[1])
    return int(any(0 == ins[2] for i in g))


def b4_uses_ref_force(w, ref, ins):
    S = w['slots']
    g = md.dll_search(w, ref, ins[1])
    if not g:
        return 0
    return int(md.sg(S[ref]['force_z']) > 0)


def b5_uses_ref_force(w, ref, ins):
    S = w['slots']
    g = md.dll_search(w, ref, ins[1])
    if not g:
        return 0
    return int(md.sg(S[ref]['force_z']) < 0)


def b6_uses_ref_force(w, ref, ins):
    S = w['slots']
    g = md.dll_search(w, ref, ins[1])
    if not g:
        return 0
    return int(md.sg(S[ref]['force_z']) == 0)


def b6_ignores_empty_group_and_reads_ref(w, ref, ins):
    S = w['slots']
    return int(md.sg(S[ref]['force_z']) == 0)


MUT = {
    0xA9: {'by_ref_id': a9_by_refid, 'excl_player': a9_excl_player},
    0xAA: {'by_ref_id': aa_by_refid, 'excl_player': aa_excl_player},
    0xB2: {'second_search_from_hero': b2_second_from_hero, 'first_search_from_hero': b2_first_from_hero},
    0xB4: {'reads_reference_force_not_group': b4_uses_ref_force},
    0xB5: {'reads_reference_force_not_group': b5_uses_ref_force},
    0xB6: {'reads_reference_force_not_group': b6_uses_ref_force, 'reads_ref_force_no_empty_guard': b6_ignores_empty_group_and_reads_ref},
    0xB7: {'compares_CurrentAnimationId_instead_of_Target': b7_status_ignored_member},
}

for op, muts in MUT.items():
    for name, fn in muts.items():
        killers = []
        for v in hand:
            if int(v['op'], 16) != op:
                continue
            ex = v['expect']['result']
            got = fn(world(v), v['ref'], instr(v))
            if got != ex:
                killers.append(v['id'])
        print('0x%02X %-48s hand killers: %d %s' % (op, name, len(killers), killers[:4]))
