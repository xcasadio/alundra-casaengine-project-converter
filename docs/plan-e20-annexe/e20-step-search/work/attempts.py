"""attempts.py - number of attempts of the halving loop for a step that is shortened and never fully accepted (worst case) and the sequence of tried steps."""
import sys, os
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)


def seq(s2, s1, cardinal):
    out = []
    while True:
        out.append((s2, s1))
        s2 = 0 if s2 == -1 else s2 >> 1
        s1 = 0 if s1 == -1 else s1 >> 1
        if cardinal:
            if s2 == 0 and s1 == 0:
                break
        else:
            if s2 == 0 or s1 == 0:
                break
    return out


lines = []
for name, s2, s1, card in (("east 2.4375 px", 159744, 0, True), ("west 2.4375 px", -159744, 0, True), ("east 3 px", 196608, 0, True), ("east 6 px", 393216, 0, True),
                           ("east 1.21875 px", 79872, 0, True), ("diag 0.8617/0.5745 (+,+)", 56473, 37652, False), ("diag (-,-)", -56473, -37652, False),
                           ("east 7 raw", 7, 0, True), ("west 7 raw", -7, 0, True), ("slide 0xC000", 0xC000, 0, True), ("slide -0xC000", -0xC000, 0, True), ("slide 0x8000 (y)", 0, 0x8000, True)):
    sq = seq(s2, s1, card)
    lines.append("%-26s attempts %2d ; last 4 steps %s" % (name, len(sq), [x if s1 == 0 else x for x in sq[-4:]]))
print("\n".join(lines))
open(os.path.join(HERE, "..", "out", "attempts.txt"), "w", encoding="utf-8").write("\n".join(lines) + "\n")
