"""Read-only. Is a seam event-exact?  Each iteration k starts at the log entry where the event at the stored loop
position runs (first pass: frame LS; after jump k: frame Jk, same frame as the 0x1E event, delay 0, 0x8008CB48).
For L frames on each side of an iteration start, compares the processed events (data position, frame offset from
the start frame):
  after(k)  = events from the iteration-start entry on, frames < f_k + L
  before(k) = events before the iteration-start entry, frames >= f_k - L
Seam B (file [0, J2), loop [J1, J2)): after(J1) == after(J2) and before(J1) == before(J2).
Seam A (file [0, J1], loop [LS, J1)): after(LS) == after(J1) and before(LS) == before(J1).
Seam B3 (file [0, J3), loop [J2, J3)): after(J2) == after(J3) and before(J2) == before(J3).
Usage: python seamexact.py [L] """
import sys, os
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import loopmodel as lm


def starts(s):
    lp = s.loop_pos[0]
    return [i for i, (fr, pos) in enumerate(s.log) if pos == lp]


def after(s, i, L):
    f = s.log[i][0]
    out = []
    for fr, pos in s.log[i:]:
        if fr >= f + L:
            break
        out.append((pos, fr - f))
    return out


def before(s, i, L):
    f = s.log[i][0]
    out = []
    for fr, pos in reversed(s.log[:i]):
        if fr < f - L:
            break
        out.append((pos, fr - f))
    return out


if __name__ == "__main__":
    L = int(sys.argv[1]) if len(sys.argv) > 1 else 180
    cnt = {"A": 0, "B": 0, "B3": 0}
    for n in range(1, 47):
        off, s = lm.simulate(n, iterations=4)
        if not s.jumps:
            continue
        st = starts(s)  # [LS entry, J1 entry, J2 entry, J3 entry, ...]
        if len(st) < 4:
            print(n, "not enough iterations", len(st))
            continue
        res = {}
        for name, (a, b) in (("A", (0, 1)), ("B", (1, 2)), ("B3", (2, 3))):
            aft = after(s, st[a], L) == after(s, st[b], L)
            bef = before(s, st[a], L) == before(s, st[b], L)
            res[name] = (aft, bef)
            cnt[name] += aft and bef
        print("%2d  A[LS,J1): after=%-5s before=%-5s  B[J1,J2): after=%-5s before=%-5s  B3[J2,J3): after=%-5s before=%-5s" % (
            n, *res["A"], *res["B"], *res["B3"]))
    print("L=%d  event-exact seams of 38 looping tracks: %s" % (L, cnt))
