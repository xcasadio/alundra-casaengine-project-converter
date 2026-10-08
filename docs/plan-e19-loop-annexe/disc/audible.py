"""Read-only. For each looping track: does iteration 1 (entered from the intro, what bgm_NNN.wav holds between the
loop start LS and the jump J1) SOUND different from iteration 2+ (entered from the jump, the steady state the
original repeats forever)?  For every channel-state key that differs between loop entry and jump (seam.analyse),
counts the note-ons on that channel inside the window where the two iterations disagree (offset < first rewrite
of that key in the iteration). Held-note differences are listed with their release offset.
Prints one line per track; 'audible' = at least one note-on plays under a different volume/pan/bend/program.
Also checks that iteration 2 and 3 are identical in state (steady state reached at J1)."""
import sys, os
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import seam
import loopmodel as lm

if __name__ == "__main__":
    idxs = [int(a) for a in sys.argv[1:]] or list(range(1, 47))
    for n in idxs:
        r, s = seam.analyse(n)
        if r["kind"] != "loop":
            continue
        LS, J = r["LS"], r["J"]
        it1 = [(fr - LS, ch, k, v) for fr, tk, ch, k, v in s.notes if LS <= fr < J[0]]
        parts = []
        audible = False
        for key, a, b, o in r["state_diffs"]:
            if key == "T":
                parts.append("T %s->%s until +%s" % (a, b, o))
                continue
            ch, kind = key
            lim = o if o is not None else J[0] - LS
            cnt = sum(1 for off, c, k, v in it1 if c == ch and off < lim)
            if cnt:
                audible = True
            parts.append("ch%d %s entry=%s jump=%s until +%s: %d notes" % (ch, kind, a, b, o, cnt))
        for tag, ch, k, o in r["held_diffs"]:
            parts.append("%s ch%d key%d released +%s" % (tag, ch, k, o))
            if tag == "loop-end-held":
                audible = True
        # steady state check
        e2 = s.snap.get("jump2")
        same23 = None
        if e2:
            st1, act1 = s.snap["jump1"]
            st2, act2 = e2
            d = [k for k in set(st1) | set(st2) if st1.get(k) != st2.get(k) and not (isinstance(k, tuple) and k[1] in ("cc98", "cc6"))]
            same23 = (not d) and act1 == act2
        print("%2d %s steady@J1=%s W=%s | %s" % (n, "AUDIBLE" if audible else "same   ", same23, r["W_frames"], "; ".join(parts)))
