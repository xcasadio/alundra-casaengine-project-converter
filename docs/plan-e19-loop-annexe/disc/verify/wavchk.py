"""WAV-side checks (read-only): header/rate, Frames vs file length, total sizes today vs option B, one-shot tails,
naive-seam vs true step, track 14 cap."""
import json, os, struct, math
from seqsim import run

ROOT = r"D:\development\repo\alundra-casaengine-project-converter\data-extracted\sound"
bj = json.load(open(os.path.join(ROOT, "bgm.json")))
print("bgm.json keys:", sorted(bj[0].keys()))
SPF = 735


def wav(path):
    b = open(path, "rb").read()
    assert b[:4] == b"RIFF" and b[8:12] == b"WAVE"
    p = 12
    fmt = None
    while p < len(b):
        cid, sz = b[p:p + 4], struct.unpack_from("<I", b, p + 4)[0]
        if cid == b"fmt ":
            fmt = struct.unpack_from("<HHIIHH", b, p + 8)
        if cid == b"data":
            return fmt, p + 8, sz, b
        p += 8 + sz + (sz & 1)


tot_now = 0
tot_b = 0
rows = []
for r in bj:
    n = r["SoundIndex"]
    path = os.path.join(ROOT, "bgm", r["File"]) if not os.path.isabs(r["File"]) else r["File"]
    if not os.path.exists(path):
        path = os.path.join(ROOT, r["File"])
    size = os.path.getsize(path)
    fmt, doff, dsz, b = wav(path)
    frames_file = dsz // 4 / SPF
    tot_now += size
    _, s = run(n, "ana")
    if s.jumps:
        newsize = s.jumps[1] * SPF * 4 + doff
    else:
        newsize = size
    tot_b += newsize
    rows.append((n, r["File"], fmt, doff, r["Frames"], frames_file, r["LoopDetected"]))
    if not s.jumps:
        # one-shot tail: RMS / peak of the last frame
        last = struct.unpack_from("<%dh" % (SPF * 2), b, doff + dsz - SPF * 4)
        rms = math.sqrt(sum(x * x for x in last) / len(last))
        print("one-shot %d last-frame rms %.0f peak %d" % (n, rms, max(abs(x) for x in last)))
    elif r["Frames"] == s.jumps[0] + 1:
        LS, J1 = s.loop_first, s.jumps[0]

        def smp(i):
            return struct.unpack_from("<hh", b, doff + i * 4)
        last = smp(J1 * SPF - 1)
        true_next = smp(J1 * SPF)
        seam_next = smp(LS * SPF)
        st = max(abs(true_next[c] - last[c]) for c in range(2))
        ss = max(abs(seam_next[c] - last[c]) for c in range(2))
        rows[-1] = rows[-1] + (st, ss)
print("fmt (tag, ch, rate, byterate, align, bits) set:", set(r[2] for r in rows), "data offsets:", set(r[3] for r in rows))
print("Frames == file frames on all:", all(abs(r[4] - r[5]) < 1e-9 for r in rows))
print("total today %.1f MB (MiB %.1f), option B %.1f MB (MiB %.1f)" % (tot_now / 1e6, tot_now / 2 ** 20, tot_b / 1e6, tot_b / 2 ** 20))
m = [r for r in rows if len(r) > 7]
twice = [r[0] for r in m if r[8] > 2 * r[7]]
print("measurable", len(m), "naive step > 2x true step:", len(twice))
for r in m:
    if r[0] in (4, 29, 24, 25):
        print("  track", r[0], "true", r[7], "naive", r[8])
print([r for r in rows if r[0] == 14])
