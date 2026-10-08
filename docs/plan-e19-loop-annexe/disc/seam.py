"""Read-only. Per-track loop table (loops.tsv) plus seam evidence.

Model side (loopmodel.py): loop-start frame LS (frame in which the event at the stored loop position first runs),
jump frames J1..J3, periods, the musical divergence window W after the loop start (how many frames, from the loop
start, until iteration 1 (entered from the intro) and iteration 2 (entered from the jump) have the same channel
state and the same held notes), and the per-event +-1 frame jitter between iterations.

WAV side (data-extracted/sound/bgm/bgm_NNN.wav, 44100 Hz, 735 stereo samples per frame, Program.cs:255/232):
the WAV holds frames [0, J1] (Frames = J1 + 1: the jump frame J1, first frame of iteration 2, is kept, Program.cs:301-306).
So the extractor already rendered ONE frame of the true post-jump audio. Compared:
  - seam step: |S[LS*735] - S[J1*735 - 1]| (naive loop [LS, J1)) vs the true step |S[J1*735] - S[J1*735 - 1]|;
  - frame RMS of (frame J1 - frame LS) relative to the RMS of frame J1.
Caveat: tracks 2-46 carry the previous track's residue at their start (O-E19-61), which reaches LS when LS < ~40.
"""
import sys, os, json, struct, math, wave
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import loopmodel as lm

SPF = 735
BGMDIR = r"D:\development\repo\alundra-casaengine-project-converter\data-extracted\sound\bgm"
BGMJSON = r"D:\development\repo\alundra-casaengine-project-converter\data-extracted\sound\bgm.json"


def read_wav(n):
    p = os.path.join(BGMDIR, "bgm_%03d.wav" % n)
    if not os.path.exists(p):
        return None
    w = wave.open(p, "rb")
    assert w.getnchannels() == 2 and w.getsampwidth() == 2 and w.getframerate() == 44100
    nf = w.getnframes()
    raw = w.readframes(nf)
    w.close()
    import array
    a = array.array("h")
    a.frombytes(raw)
    return a  # interleaved L R


def frame_slice(a, f):
    return a[f * SPF * 2:(f + 1) * SPF * 2]


def rms(xs):
    return math.sqrt(sum(x * x for x in xs) / len(xs)) if xs else 0.0


def first_offset(log, key, start, end):
    for fr, k in log:
        if start <= fr < end and k == key:
            return fr - start
    return None


def equivalent(key, a, b):
    if a == b:
        return True
    # pitch bend never written vs centre (0, 64): same sound (assumption: the driver's reset value is the centre)
    if isinstance(key, tuple) and key[1] == "bend" and {a, b} <= {None, (0, 64)}:
        return True
    return False


def analyse(n):
    off, s = lm.simulate(n, iterations=3, max_frames=60 * 60 * 30)
    r = {"idx": n, "seq_off": "%#x" % off}
    bj = {e["SoundIndex"]: e for e in json.load(open(BGMJSON))}.get(n, {})
    r["bgm_frames"] = bj.get("Frames")
    r["first_note_frame"] = s.notes[0][0] if s.notes else None
    if not s.jumps:
        r["kind"] = "one-shot" if s.ended_frame is not None else "no-end"
        r["end_frame"] = s.ended_frame
        return r, s
    r["kind"] = "loop"
    LS = s.lp_first_frame
    J = s.jumps
    r["LS"] = LS
    r["LS_tick"] = s.lp_first_tick
    r["J"] = J
    r["P"] = [J[0] - LS, J[1] - J[0], J[2] - J[1]]
    entry_state, entry_active = s.snap["entry"]
    jump_state, jump_active = s.snap["jump1"]
    # state keys that differ (ignoring the loop-count re-latch noise and the bend None/centre equivalence)
    diffs = []
    W_state = 0
    for key in sorted(set(entry_state) | set(jump_state), key=str):
        if key in ((0, "cc98"),) or (isinstance(key, tuple) and key[1] in ("cc98", "cc6")):
            continue
        a, b = entry_state.get(key), jump_state.get(key)
        if equivalent(key, a, b):
            continue
        if key == "T":
            # first tempo change inside iteration 1
            o = None
            for tk, tv, T, fr in s.tempo_changes:
                if LS <= fr < J[0]:
                    o = fr - LS
                    break
        else:
            o = first_offset(s.setlog, key, LS, J[0])
        diffs.append((key, a, b, o))
        W_state = max(W_state, o if o is not None else 10 ** 9)
    r["state_diffs"] = diffs
    # held notes: entry-held released in iteration 1, jump-held released in iteration 2
    W_notes = 0
    held = []
    for (ch, k) in sorted(entry_active ^ jump_active):
        if (ch, k) in entry_active:
            o = first_offset(s.offlog, (ch, k), LS, J[0])
            held.append(("intro-held", ch, k, o))
        else:
            o = first_offset(s.offlog, (ch, k), J[0], J[1])
            held.append(("loop-end-held", ch, k, o))
        W_notes = max(W_notes, o if o is not None else 10 ** 9)
    r["held_diffs"] = held
    r["W_frames"] = max(W_state, W_notes)
    # jitter
    it1 = lm.iteration_offsets(s, LS, J[0])
    it2 = lm.iteration_offsets(s, J[0], J[1])
    m = min(len(it1), len(it2))
    shifted = [(it1[i][1], it2[i][1] - it1[i][1]) for i in range(m) if it1[i][0] == it2[i][0] and it1[i][1] != it2[i][1]]
    r["events_iter"] = len(it1)
    r["events_shifted_1_2"] = len(shifted)
    r["shift_values"] = sorted(set(x[1] for x in shifted))
    # WAV seam evidence
    a = read_wav(n)
    if a is not None and len(a) // (2 * SPF) == J[0] + 1:
        jf = frame_slice(a, J[0])
        lf = frame_slice(a, LS)
        d = [jf[i] - lf[i] for i in range(len(jf))]
        r["wav_rms_jump_frame"] = round(rms(jf), 1)
        r["wav_rms_diff_jump_vs_LS"] = round(rms(d), 1)
        last = a[(J[0] * SPF - 1) * 2:(J[0] * SPF) * 2]
        true_next = a[(J[0] * SPF) * 2:(J[0] * SPF + 1) * 2]
        seam_next = a[(LS * SPF) * 2:(LS * SPF + 1) * 2]
        r["step_true"] = max(abs(true_next[c] - last[c]) for c in range(2))
        r["step_seam"] = max(abs(seam_next[c] - last[c]) for c in range(2))
    else:
        r["wav_note"] = "WAV frames != J1+1 (track 14: capped at 14400)" if a is not None else "no WAV"
    # does iteration 1 sound different from the steady state? (notes under a differing channel state, or a note
    # held across the jump)
    it1n = [(fr - LS, ch) for fr, tk, ch, k, v in s.notes if LS <= fr < J[0]]
    why = []
    bend_only = []
    for key, a_, b_, o in diffs:
        if key == "T":
            continue
        ch, kind = key
        lim = o if o is not None else J[0] - LS
        c = sum(1 for off_, ch_ in it1n if ch_ == ch and off_ < lim)
        if c and kind == "bend":
            # a stored bend does not reach new notes: 0xE0 -> FUN_8008D4C0 -> FUN_80093030 bends the voices
            # sounding at that moment only (SoundManager.cs:1462-1474, 3119-3137, 3187-3232); the note-on reads
            # channel volume and pan (:4278, :3113) but no bend
            bend_only.append("ch%d bend %s->%s (%d notes before +%s, new notes unaffected)" % (ch, a_, b_, c, o))
        elif c:
            why.append("ch%d %s %s->%s %d notes before +%s" % (ch, kind, a_, b_, c, o))
    r["bend_state_only"] = bend_only
    for tag, ch, k, o in held:
        if tag == "loop-end-held":
            why.append("ch%d key%d held across the jump (released +%s)" % (ch, k, o))
    r["iter1_differs"] = why
    st1, act1 = s.snap["jump1"]
    st2, act2 = s.snap["jump2"]
    d12 = [k for k in set(st1) | set(st2) if st1.get(k) != st2.get(k) and not (isinstance(k, tuple) and k[1] in ("cc98", "cc6"))]
    r["steady_at_J1"] = (not d12) and act1 == act2
    return r, s


def main(idxs):
    rows = []
    for n in idxs:
        r, s = analyse(n)
        rows.append(r)
    # Column meanings (frames are 60 Hz sequencer frames = WAV frames of 735 stereo samples at 44100 Hz):
    #  LS = loop-start frame (event at the stored loop position, first pass: iteration 1 starts; intro = [0, LS))
    #  J1, J2 = frames of the 1st and 2nd backward jumps (iteration 2 / 3 start; the loop-position event runs in
    #  the jump frame itself). Today's bgm.json Frames = J1 + 1.
    #  Option A (naive): loop [LS, J1) of today's WAV.  Option B (steady state): file [0, J2), loop [J1, J2).
    cols = ["idx", "seq_off", "kind", "first_note_frame", "LS_tick", "LS_frame", "LS_sample", "intro_s",
            "J1_frame", "J1_sample", "J2_frame", "J2_sample", "P1_frames", "P2_frames", "P3_frames", "loop_s",
            "bgm_json_Frames_today", "optA_loop_start_sample", "optA_loop_end_sample",
            "optB_file_frames", "optB_loop_start_sample", "optB_loop_end_sample",
            "iter1_differs_from_steady_state", "steady_state_at_J1", "divergence_W_frames", "events_shifted_iter1_vs_2",
            "shift_values", "wav_rms_frameJ1", "wav_rms_frameJ1_minus_frameLS", "wav_step_true_at_J1", "wav_step_naive_seam",
            "end_frame", "used_by_map_entry", "used_by_0xA7", "notes"]
    usage = {}
    p = os.path.join(HERE, "a7scan.json")
    if os.path.exists(p):
        usage = json.load(open(p))
    with open(os.path.join(HERE, "loops.tsv"), "w", encoding="utf-8") as f:
        f.write("\t".join(cols) + "\n")
        for r in rows:
            u = usage.get(str(r["idx"]), {"map_entry": [], "a7": []})
            r["_maps"] = ",".join(map(str, u["map_entry"]))
            r["_a7"] = ",".join(sorted(set("%s(%s,%s)" % (m, r["idx"], v2) for m, nm, v2, a in u["a7"])))
            if r["kind"] == "loop":
                LS, J, P = r["LS"], r["J"], r["P"]
                notes = list(r["iter1_differs"])
                if r.get("wav_note"):
                    notes.append(r["wav_note"])
                if r["bgm_frames"] != J[0] + 1:
                    notes.append("bgm.json Frames %s != J1+1 %d" % (r["bgm_frames"], J[0] + 1))
                vals = [r["idx"], r["seq_off"], "loop(count=127)", r["first_note_frame"], r["LS_tick"], LS, LS * SPF,
                        "%.3f" % (LS / 60), J[0], J[0] * SPF, J[1], J[1] * SPF, P[0], P[1], P[2], "%.2f" % (P[1] / 60),
                        r["bgm_frames"], LS * SPF, J[0] * SPF, J[1], J[0] * SPF, J[1] * SPF,
                        "yes" if r["iter1_differs"] else "no", r["steady_at_J1"],
                        r["W_frames"] if r["W_frames"] < 10 ** 9 else "never",
                        r["events_shifted_1_2"], ",".join(map(str, r["shift_values"])), r.get("wav_rms_jump_frame", ""),
                        r.get("wav_rms_diff_jump_vs_LS", ""), r.get("step_true", ""), r.get("step_seam", ""), "",
                        r["_maps"], r["_a7"], "; ".join(notes)]
            else:
                vals = [r["idx"], r["seq_off"], r["kind"], r["first_note_frame"]] + [""] * 12 + [r["bgm_frames"]] + [""] * 14 + [
                        r.get("end_frame", ""), r["_maps"], r["_a7"], "no CC99=20/30 loop: end-of-track meta 0x2F with l_count 1 -> sequence stops, voices keyed off (0x8008D77C-0x8008D8B4); plays once"]
            f.write("\t".join("" if v is None else str(v) for v in vals) + "\n")
    with open(os.path.join(HERE, "seam_details.jsonl"), "w", encoding="utf-8") as f:
        for r in rows:
            f.write(json.dumps(r, default=str) + "\n")
    return rows


if __name__ == "__main__":
    idxs = [int(a) for a in sys.argv[1:]] or list(range(1, 47))
    rows = main(idxs)
    for r in rows:
        if r["kind"] != "loop":
            print(r["idx"], r["kind"], "end", r.get("end_frame"), "bgmF", r["bgm_frames"])
            continue
        print(r["idx"], "LS", r["LS"], "J", r["J"], "P", r["P"], "W", r["W_frames"], "shift", r["events_shifted_1_2"],
              r["shift_values"], "rmsJ", r.get("wav_rms_jump_frame"), "rmsdiff", r.get("wav_rms_diff_jump_vs_LS"),
              "step", r.get("step_true"), r.get("step_seam"), r.get("wav_note", ""))
        print("    state", [(str(k), a, b, o) for k, a, b, o in r["state_diffs"]])
        print("    held", r["held_diffs"])
