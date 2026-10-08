"""Read-only. Correlation of the WAV's jump frame J1 (first frame of iteration 2, the only post-jump audio the
extractor keeps) with frames LS-3..LS+3 and with the frame before J1, per track. Also per-frame RMS around LS and
J1, to see whether the jump frame resembles the loop-start frame (same new notes) or is dominated by other content.
Usage: python corr.py [idx ...]"""
import sys, os, math
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import seam
import loopmodel as lm

SPF = 735


def corr(x, y):
    n = min(len(x), len(y))
    mx = sum(x[:n]) / n
    my = sum(y[:n]) / n
    sxy = sum((x[i] - mx) * (y[i] - my) for i in range(n))
    sxx = sum((x[i] - mx) ** 2 for i in range(n))
    syy = sum((y[i] - my) ** 2 for i in range(n))
    return sxy / math.sqrt(sxx * syy) if sxx > 0 and syy > 0 else 0.0


def chan(a, f, c):
    s = seam.frame_slice(a, f)
    return [s[i] for i in range(c, len(s), 2)]


if __name__ == "__main__":
    idxs = [int(x) for x in sys.argv[1:]] or [24, 25, 2, 33, 20, 35]
    for n in idxs:
        off, s = lm.simulate(n)
        if not s.jumps:
            continue
        LS, J1 = s.lp_first_frame, s.jumps[0]
        a = seam.read_wav(n)
        if a is None or len(a) // (2 * SPF) != J1 + 1:
            print(n, "skip")
            continue
        jl, jr = chan(a, J1, 0), chan(a, J1, 1)
        row = []
        for d in range(-3, 4):
            f = LS + d
            row.append("%+d:%.2f/%.2f" % (d, corr(jl, chan(a, f, 0)), corr(jr, chan(a, f, 1))))
        prev = "%.2f/%.2f" % (corr(jl, chan(a, J1 - 1, 0)), corr(jr, chan(a, J1 - 1, 1)))
        rms_ls = [round(seam.rms(seam.frame_slice(a, f))) for f in range(LS - 2, LS + 4)]
        rms_j = [round(seam.rms(seam.frame_slice(a, f))) for f in range(J1 - 3, J1 + 1)]
        print(n, "LS", LS, "J1", J1, "corr(J1, LS+d) L/R", " ".join(row), "| corr(J1,J1-1)", prev)
        print("    rms LS-2..LS+3", rms_ls, " rms J1-3..J1", rms_j)
