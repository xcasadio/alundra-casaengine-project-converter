"""Read-only. WAV size impact of the options (44100 Hz stereo 16 bit: 735 samples x 4 bytes = 2940 bytes per frame,
plus the 44-byte header the extractor writes). Today: Frames = J1 + 1 (looping) or end + 1 (one-shot).
Option B: file = [0, J2) for looping tracks; one-shots unchanged here (a release tail would add a little).
Also the largest J2 (the extractor's 240 s cap must cover it) and the resident memory of route B (PlayClip)."""
import sys, os, json
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import loopmodel as lm

BPF = 735 * 4
today = optB = 0
maxJ2 = (0, None)
for n in range(1, 47):
    off, s = lm.simulate(n, iterations=2)
    bj = {e["SoundIndex"]: e for e in json.load(open(lm.BGMJSON))}[n]
    today += 44 + bj["Frames"] * BPF
    if s.jumps:
        J1, J2 = s.jumps[0], s.jumps[1]
        optB += 44 + J2 * BPF
        if J2 > maxJ2[0]:
            maxJ2 = (J2, n)
    else:
        optB += 44 + bj["Frames"] * BPF
print("today %.1f MB, option B %.1f MB (x%.2f); largest J2 = %d frames (%.1f s) on track %d" % (
    today / 2 ** 20, optB / 2 ** 20, optB / today, maxJ2[0], maxJ2[0] / 60, maxJ2[1]))
for n in (25, 1, 7, 14, 35):
    off, s = lm.simulate(n, iterations=2)
    J1, J2 = s.jumps[0], s.jumps[1]
    print("track %d: today %.1f MB (J1+1=%d frames), option B %.1f MB (J2=%d frames)" % (
        n, (44 + (J1 + 1) * BPF) / 2 ** 20, J1 + 1, (44 + J2 * BPF) / 2 ** 20, J2))
