"""c3_contact.py - CRITIC: when exactly does a Square edge lift?  The contact (hero +0x130) is the entity that blocked the hero's step of the PREVIOUS tick.
Cases (crate at x=130, hero starts at x=100 walking east, blocked from t5):
  A  hold RIGHT t1..t9, release at t10 and press Square at t10            -> contact of t9 still there?
  B  hold RIGHT t1..t9, release at t10, press Square at t11               -> contact of t10 (no step asked) = 0 ?
  C  hold RIGHT t1..t9, press Square at t10 while RIGHT is still held     -> lift
  D  hold RIGHT t1..t9, release, wait 5 ticks, hold RIGHT again, Square on the first tick of the second hold (contact comes from the NEXT physics, so t+1)
  E  Square pressed at the same tick as the hero first holds RIGHT next to the crate (touching, no contact yet)
  F  hold RIGHT with the Square key HELD from t1 (no edge ever) -> never lifts
  G  hold UP (north) while the crate is east (hero not pushing into it): Square -> sword
Output: the hero target animation after the Square tick and whether hero+0x2C is set.   Harness writes: the pad only.
Run: python -B -I c3_contact.py"""
import sys, os
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.join(HERE, 'oracle'))
import gl

R, SQ, UP = gl.RIGHT, gl.SQUARE, gl.UP


def run(seq, n=None):
    m = gl.world(hero_px=(100, 100), direction=24)
    c = gl.add_crate(m, 130, 100)
    pads, nn = gl.pad_script(seq)
    rows = gl.run(m, n or nn, pads, crates=[c])
    return rows


def verdict(rows, sq_tick):
    """what the Square at tick sq_tick did: sword (anim 0x10 family) or lift (carried2C set, anim 5)"""
    seen = []
    for r in rows:
        if r['tick'] >= sq_tick and r['tick'] <= sq_tick + 3:
            seen.append((r['tick'], r['hero']['anim_t'], r['hero']['carried2C'] != 0, r['hero']['xcoll'] != 0))
    lifted = any(s[2] for s in seen)
    return 'LIFT' if lifted else 'no lift (anim after: %s)' % [s[1] for s in seen]


def main():
    out = []
    cases = {
        'A hold RIGHT t1-9, release+Square at t10': ([(9, R), (1, SQ), (10, 0)], 10),
        'B hold RIGHT t1-9, release at t10, Square at t11': ([(9, R), (1, 0), (1, SQ), (10, 0)], 11),
        'C hold RIGHT t1-9, Square at t10 still holding RIGHT': ([(9, R), (1, R | SQ), (10, R)], 10),
        'D release t10-14, RIGHT again t15, Square at t15 (first tick of the 2nd hold)': ([(9, R), (5, 0), (1, R | SQ), (10, R)], 15),
        'D2 same with Square at t16': ([(9, R), (5, 0), (1, R), (1, R | SQ), (10, R)], 16),
        'E Square at t5 (first tick touching: contact appears after the physics of t5)': ([(4, R), (1, R | SQ), (10, R)], 5),
        'E2 Square at t6': ([(5, R), (1, R | SQ), (10, R)], 6),
        'F Square HELD from t1 with RIGHT': ([(20, R | SQ)], 6),
        'G UP held (not pushing the crate), Square at t10': ([(9, UP), (1, UP | SQ), (10, 0)], 10),
    }
    for name, (seq, sq) in cases.items():
        rows = run(seq)
        out.append('%-84s %s' % (name, verdict(rows, sq)))
    txt = '\n'.join(out)
    print(txt)
    open(os.path.join(gl.OUT, 'c3-contact.txt'), 'w').write(txt + '\n')


if __name__ == '__main__':
    main()
