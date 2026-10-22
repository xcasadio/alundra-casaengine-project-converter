"""E19.sm4: mutation matrix of the HANDLER-LEVEL rules (R1 0x72, R2 0x88, R3 0x96, R4 0x97, R5 0xB3), computed on the python model of the
rule against (a) the 43 named unit tests (their expected fields, audited against the binary by named_tests.py) and (b) the 348 replayable
vectors. A mutant is KILLED by a test when the mutated rule gives, for that test's start state, a value different from the one the test asserts.
The structural mutants (HUD snap, DestroyEntity side effects, the stats written by 0x96, the seam order) are not computable here: they are
listed in the report with the test that kills them by construction.

Usage: python -B mutants.py VECTORS_DIR OUT.json
"""
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import sm4model as S  # noqa: E402
import named_tests as N  # noqa: E402

M32 = S.M32


def clone_world(w):
    return dict(slots=[dict(s) for s in w['slots']], stats=dict(w['stats']), pad=dict(w['pad']) if w['pad'] else None, state=w['state'])


def rule(op, w, logic, owner, instr, mutant=None):
    """The rule of the slice (mutant None) or one mutant; returns the FINAL logical fields: slot fields, stats, pad."""
    w = clone_world(w)
    slots = w['slots']
    b = list(instr) + [0] * 12
    ref = logic
    if op == 0x72:
        tgt = owner if mutant == 'M72b' else logic
        s = slots[tgt]
        if mutant == 'M72d' and not (1 <= s['status'] <= 3):
            return w
        if mutant == 'M72c':
            s['targetAnimId'], s['targetDir'] = s['lastTargetAnimId'], s['lastTargetDir']
        else:
            s['lastTargetAnimId'] = s['targetAnimId']
            if mutant != 'M72a':
                s['lastTargetDir'] = s['targetDir']
    elif op == 0x88:
        ref = owner if mutant == 'M88f' else logic
        ms = S.search(w, ref, b[1])
        idx = b[2]
        table = [2, 3, 4]
        if mutant == 'M88a':
            table = [3, 2, 4]
        if idx >= 3:
            if mutant == 'M88e':
                table_val = 0
            else:
                return w
        else:
            table_val = table[idx]
        if mutant == 'M88c':
            ms = ms[:1]
        for i in ms:
            slots[i]['status'] = table_val
    elif op == 0x96:
        amount = b[2] if mutant == 'M96a' else b[1]
        ms = S.search(w, ref, b[1])
        for i in ms:
            s = slots[i]
            if mutant == 'M96f' and not (1 <= s['status'] <= 3):
                continue
            if mutant == 'M96g' and s['hp'] == 0:
                continue
            if mutant == 'M96e':
                total = s['hp'] + amount
                s['hp'] = min(s['hpMax'], total)
                continue
            total = S.s32(s['hp'] + amount)
            normal = s['hpMax'] if s['hpMax'] < total else total
            if mutant == 'M96b':
                s['hp'] = total
            elif mutant == 'M96c':
                s['hp'] = max(0, normal)
            elif mutant == 'M96d':
                s['hp'] = s['hp'] if s['hp'] > s['hpMax'] else normal
            else:
                s['hp'] = normal
    elif op == 0x97:
        st = w['stats']
        mp, mpmax = S.s16(st['mp']), S.s16(st['mpMax'])
        v = mp + b[1]
        if mutant == 'M97a':
            st['money'] = max(0, min(9999, S.s16(st['money']) - b[1]))
            return w
        if mutant == 'M97b':
            v = S.s16(v)
        if mutant == 'M97e':
            mpmax = 4
        if mpmax < v:
            new = mpmax
        elif v < 0 and mutant != 'M97c':
            new = 0
        else:
            new = v
        if mutant == 'M97d':
            new = S.s16(mp + b[1])
        st['mp'] = S.s16(new)
    elif op == 0xB3:
        pad = dict(w['pad'])
        words = dict(hold=b[1] | b[2] << 8, pressed=b[3] | b[4] << 8, released=b[5] | b[6] << 8, interval=b[7] | b[8] << 8)
        if mutant == 'MB3a':
            words = dict(hold=words['hold'], pressed=pad['pressed'], released=pad['released'], interval=pad['interval'])
        if mutant == 'MB3b':
            words = {k: (pad[k] | v) for k, v in words.items()}
        if mutant == 'MB3c':
            words = dict(hold=b[2] | b[1] << 8, pressed=b[4] | b[3] << 8, released=b[6] | b[5] << 8, interval=b[8] | b[7] << 8)
        if mutant == 'MB3d':
            words['pressed'], words['released'] = words['released'], words['pressed']
        if mutant == 'MB3e':
            words = {k: v & 0xFF for k, v in words.items()}
        w['pad'] = words
    return w


def observe(w):
    out = {'slot': {}, 'stats': {}, 'pad': {}}
    for i, s in enumerate(w['slots']):
        for name in (v[0] for v in S.SLOT_FIELDS.values()):
            out['slot'][(i, name)] = s[name]
    for k, v in w['stats'].items():
        out['stats'][k] = S.s16(v)
    for k, v in (w['pad'] or {}).items():
        out['pad'][k] = v
    return out


MUTANTS = {
    0x72: [('M72a', 'the direction is not copied'), ('M72b', 'acts on the owner, not on the logic entity'), ('M72c', 'copies the other way (Target := Last)'),
           ('M72d', 'guarded by a status 1..3')],
    0x88: [('M88a', 'table with Normal and Deactivated swapped'), ('M88c', 'only the first match is written'), ('M88e', 'an index above 2 writes a word (0)'),
           ('M88f', 'the search reference is the owner, not the logic entity')],
    0x96: [('M96a', 'the amount is the second byte'), ('M96b', 'no HpMax clamp'), ('M96c', 'a floor at 0 is added'), ('M96d', 'never lowers an Hp above HpMax'),
           ('M96e', 'saturating 64-bit sum instead of the 32-bit wrap'), ('M96f', 'only status 1..3 targets are healed'), ('M96g', 'a target at Hp 0 is skipped')],
    0x97: [('M97a', 'spends money (the decompilation)'), ('M97b', 'the sum is cast to 16 bits before the clamp'), ('M97c', 'no floor at 0'),
           ('M97d', 'no MpMax clamp'), ('M97e', 'clamps to the constant 4 instead of MpMax')],
    0xB3: [('MB3a', 'only the hold word is written'), ('MB3b', 'the words are OR-ed over the previous ones'), ('MB3c', 'big-endian words'),
           ('MB3d', 'pressed and released swapped'), ('MB3e', 'words masked to 8 bits')],
}


def main():
    vdir, out_path = sys.argv[1], sys.argv[2]
    result = {}
    unit = {tc['id']: tc for tc in N.T}
    for op, muts in MUTANTS.items():
        for mid, desc in muts:
            killers = []
            for tc in N.T:
                if tc['instr'][0] != op:
                    continue
                base = observe(rule(op, tc['world'], tc['logic'], tc['owner'], tc['instr']))
                mut = observe(rule(op, tc['world'], tc['logic'], tc['owner'], tc['instr'], mid))
                # a test kills the mutant when one of ITS asserted fields differs from the expected value
                killed = False
                for cat, d in tc['expect'].items():
                    for key, val in d.items():
                        want = val
                        if cat == 'slot':
                            kind = S.SLOT_FIELDS[S.SLOT_OFF_BY_NAME[key[1]]][2]
                            want = S.s32(val) if kind == 's32' else val & M32
                        if cat == 'stats':
                            want = S.s16(val)
                        if base[cat][key] != want:
                            raise SystemExit('baseline rule disagrees with test %s %s' % (tc['id'], key))
                        if mut[cat][key] != want:
                            killed = True
                if killed:
                    killers.append(tc['id'])
            # the vectors: a mutant is killed by a vector when the final fields differ from the vector's expectation
            vkill = 0
            vtot = 0
            doc = json.load(open(os.path.join(vdir, 'op%02X.json' % op), encoding='utf-8'))
            for v in doc['vectors']:
                if v.get('outOfTable'):
                    continue
                vtot += 1
                w = dict(slots=v['world']['slots'], stats=v['world']['stats'], pad=v['world']['pad'], state={})
                instr = v['instruction']
                a = observe(rule(op, w, v['logicSlot'], v['ownerSlot'], instr))
                b = observe(rule(op, w, v['logicSlot'], v['ownerSlot'], instr, mid))
                if a != b:
                    vkill += 1
            result[mid] = dict(opcode='0x%02X' % op, description=desc, killedByUnitTests=killers, killedByVectorRows='%d/%d' % (vkill, vtot))
    json.dump(result, open(out_path, 'w', encoding='utf-8', newline='\n'), indent=1, sort_keys=True)
    survivors = [m for m, r in result.items() if not r['killedByUnitTests']]
    print('%d mutants; killed by at least one named unit test: %d; survivors (unit level): %s' % (len(result), len(result) - len(survivors), survivors))
    for m, r in sorted(result.items()):
        print(m, r['killedByUnitTests'], r['killedByVectorRows'])


if __name__ == '__main__':
    main()
