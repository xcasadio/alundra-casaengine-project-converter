"""alarm.py - E20.c2a T4: the low-life alarm of CreatePlayerAnimationEffects (0x800303DC-0x80030430) measured on the REAL code of ALUN_CD.EXE (France), in the emulator of E20.c2-0.

A NEW file next to the c2-0 oracle (nothing of the c2-0 tools, scenarios, outputs or manifest is edited): the c2-0 scenarios carry the first sound of the alarm only (A2-*-5-12, sound(400) at tick 3,
one hurt away) because none of them lives 46 ticks at a low life.  This one runs the hero at rest, with no target, at a low life, and reads the journal.
    usage:  python -B docs/plan-e20-annexe/e20-c2/oracle/alarm.py        (exit code 0 = every measured line equals the line written below BEFORE the run)
Output: out-c2a/alarm.txt (the rows read from the machine).

Rule read in the binary (0x800303DC-0x80030430, mode 0 only): hero +0x14 (Hp) != 0 and not (hero +0x18 (HpMax) < 5 * Hp)  =>  counter[0x80098F2C] -= 1; if it is -1: sound(0x190 = 400), counter := 0x2D.
Otherwise nothing is touched.  The counter starts at 0 (BSS).  Expected sound ticks (written before the run):
  AL1  Hp 2 / HpMax 12, 200 ticks                      -> [1, 47, 93, 139, 185]   (counter 0 -> -1 at tick 1, reload 45, then -1 again 46 ticks later)
  AL2a Hp 2 / 10 (HpMax == 5 Hp: active)               -> [1, 47]
  AL2b Hp 3 / 10 (10 < 15: silent)                     -> []
  AL2c Hp 10 / 50 (50 == 5 * 10: active)               -> [1, 47]
  AL2d Hp 11 / 50 (silent)                             -> []
  AL2e Hp 1 / 50 (active)                              -> [1, 47]
  AL2f Hp 0 / 10 (Hp == 0: silent), 30 ticks          -> []
  AL3  Hp 2 / 12, Hp := 10 before tick 21 (silent), := 2 before tick 41; 100 ticks -> [1, 67]
       (counter 45 after tick 1, 26 after tick 20 (19 more decrements), frozen over 21..40, 25 at tick 41, -1 at tick 41 + 26 = 67)
"""
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, 'tools'))
sys.path.insert(0, HERE)
import scen as S
import hworld as H
import hscen as HS

EXPECTED = {
    'AL1': [1, 47, 93, 139, 185],
    'AL2a': [1, 47],
    'AL2b': [],
    'AL2c': [1, 47],
    'AL2d': [],
    'AL2e': [1, 47],
    'AL2f': [],
    'AL3': [1, 67],
}


def run(name, desc, n, hp, hpmax, events=None):
    sp = S.spec(name, desc, 60, 0, n=n, targets=[], hp=hp, hpmax=hpmax, inv='kit')
    if events:
        sp['events'] = events
    return H.run(sp, 'emu', capture=False)


def alarm_ticks(res):
    return [t for (t, a) in HS.sound_ticks(res) if a == 400]


def main():
    runs = [
        ('AL1', 'Hp 2 / 12, no target, 200 ticks', 200, 2, 12, None),
        ('AL2a', 'Hp 2 / 10', 60, 2, 10, None),
        ('AL2b', 'Hp 3 / 10', 60, 3, 10, None),
        ('AL2c', 'Hp 10 / 50', 60, 10, 50, None),
        ('AL2d', 'Hp 11 / 50', 60, 11, 50, None),
        ('AL2e', 'Hp 1 / 50', 60, 1, 50, None),
        ('AL2f', 'Hp 0 / 10', 30, 0, 10, None),
        ('AL3', 'Hp 2 / 12; entity Hp := 10 before tick 21, := 2 before tick 41', 100, 2, 12, {21: [('hero_hp', 10)], 41: [('hero_hp', 2)]}),
    ]
    lines = ['# E20.c2a T4: the low-life alarm, measured on the real code (sound(400) ticks); expected values were written in alarm.py before the run']
    ok = True
    for name, desc, n, hp, hpmax, events in runs:
        res = run(name, desc, n, hp, hpmax, events)
        got = alarm_ticks(res)
        want = EXPECTED[name]
        verdict = 'PASS' if got == want else 'FAIL'
        ok = ok and got == want
        lines.append('%s  %-5s %-70s sound(400) at %s (expected %s)' % (verdict, name, desc, got, want))
    out = os.path.join(HERE, 'out-c2a')
    os.makedirs(out, exist_ok=True)
    with open(os.path.join(out, 'alarm.txt'), 'w', encoding='utf-8', newline='\n') as f:
        f.write('\n'.join(lines) + '\n')
    print('\n'.join(lines))
    return 0 if ok else 1


if __name__ == '__main__':
    sys.exit(main())
