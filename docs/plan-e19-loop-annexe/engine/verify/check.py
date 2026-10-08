"""Independent read-only checks of the L-a (engine loop start) design report.

Written from MusicPlayer.cs:273-300, WavStreamReader.cs:60-97/114-200 and FakeAudioBackend.cs:195-256 (engine a6efd2a9).
Does not import fillsim.py, loopmodel.py or seqsim.py. Reads loops.tsv (prior discovery) only to compare it with
values recomputed from the produced files (data-extracted/sound/bgm.json and the WAV headers).
"""
import json
import os
import struct

REPO = r"D:\development\repo\alundra-casaengine-project-converter"
SCR = r"C:\Users\casad\AppData\Local\Temp\claude\D--development-repo-alundra-casaengine-project-converter\b00d1a72-420d-4acc-ab34-0c25fbdad3a1\scratchpad"
SPF = 44100 // 60  # extractor Program.cs:258


def wav_header(path):
    """Own RIFF walker: returns (fmt fields, data offset, data length, chunk list)."""
    with open(path, "rb") as f:
        head = f.read(12)
        assert head[:4] == b"RIFF" and head[8:12] == b"WAVE", path
        chunks = []
        fmt = None
        size = os.path.getsize(path)
        while True:
            h = f.read(8)
            if len(h) < 8:
                break
            cid, csz = h[:4].decode("latin1"), struct.unpack("<I", h[4:])[0]
            pos = f.tell()
            chunks.append((cid, pos - 8, csz))
            if cid == "fmt ":
                tag, ch, rate, bps, align, bits = struct.unpack("<HHIIHH", f.read(16))
                fmt = dict(tag=tag, ch=ch, rate=rate, align=align, bits=bits)
                f.seek(pos + csz + (csz & 1))
                continue
            if cid == "data":
                avail = size - pos
                dlen = avail if (csz == 0 or csz > avail) else csz
                dlen -= dlen % fmt["align"]
                return fmt, pos, dlen, chunks
            f.seek(pos + csz + (csz & 1))
    raise ValueError("no data chunk")


print("== A. loops.tsv vs bgm.json (J1 = Frames - 1 for looping tracks) ==")
rows = {}
with open(os.path.join(SCR, "loop-disc", "loops.tsv"), encoding="utf-8") as f:
    hdr = f.readline().rstrip("\n").split("\t")
    for line in f:
        p = line.rstrip("\n").split("\t")
        r = dict(zip(hdr, p))
        rows[int(r["idx"])] = r
bgm = {e["SoundIndex"]: e for e in json.load(open(os.path.join(REPO, "data-extracted", "sound", "bgm.json"), encoding="utf-8"))}
mism = []
for idx, r in sorted(rows.items()):
    e = bgm.get(idx)
    if r["kind"].startswith("loop"):
        j1 = int(r["J1_frame"])
        if e is None:
            mism.append((idx, "no bgm.json entry"))
        elif e["LoopDetected"] and e["Frames"] - 1 != j1:
            mism.append((idx, f"Frames-1={e['Frames'] - 1} J1={j1}"))
        elif not e["LoopDetected"]:
            mism.append((idx, f"LoopDetected false, Frames={e['Frames']}, J1={j1}"))
    else:
        end = int(r["end_frame"])
        if e is None:
            mism.append((idx, f"one-shot, no bgm.json entry (end {end})"))
        elif e["Frames"] != end + 1:
            mism.append((idx, f"one-shot Frames={e['Frames']} end+1={end + 1}"))
print("mismatches:", mism)

print("\n== B. WAV headers of the sampled tracks (data-extracted/sound/bgm) ==")
SAMPLE = [1, 7, 14, 25, 32, 35, 45, 26, 36]
for idx in SAMPLE:
    path = os.path.join(REPO, "data-extracted", "sound", "bgm", f"bgm_{idx:03d}.wav")
    fmt, off, dlen, chunks = wav_header(path)
    frames = dlen // fmt["align"]
    e = bgm[idx]
    print(f"track {idx:2d}: off={off} rate={fmt['rate']} ch={fmt['ch']} bits={fmt['bits']} align={fmt['align']} "
          f"sampleFrames={frames} bgm.Frames*735={e['Frames'] * SPF} equal={frames == e['Frames'] * SPF} "
          f"LoopDetected={e['LoopDetected']}")
for name in ("racing_game_music_1.wav", "menu_screenclick.wav"):
    path = os.path.join(REPO, "CasaEngineMonogame", "CasaEngine.Demos", "Content", "Audio", name)
    fmt, off, dlen, chunks = wav_header(path)
    print(f"{name}: chunks={chunks} rate={fmt['rate']} data_offset={off} seconds={dlen / fmt['align'] / fmt['rate']:.3f}")

print("\n== C. values the report quotes for L-b (option B, D-E19-73: file [0,J2), loop [J1,J2)) ==")
for idx in SAMPLE + [46]:
    r = rows[idx]
    if not r["kind"].startswith("loop"):
        print(f"track {idx:2d}: one-shot, end {r['end_frame']} -> no loop start (Looping false)")
        continue
    j1, j2 = int(r["J1_frame"]), int(r["J2_frame"])
    ls = j1 * SPF
    size = j2 * SPF * 4 + 44
    region = (j2 - j1) * SPF
    print(f"track {idx:2d}: J1={j1} J2={j2} LoopStartSample={ls} fileSampleFrames={j2 * SPF} bytes={size} "
          f"({size / 2**20:.1f} MiB) loopRegion={region} frames ({region / 44100:.2f} s) loopStart<frameCount={ls < j2 * SPF}")
loopers = [i for i, r in rows.items() if r["kind"].startswith("loop")]
mx = max(int(rows[i]["J2_frame"]) * SPF for i in loopers)
mn = min((int(rows[i]["J2_frame"]) - int(rows[i]["J1_frame"]), i) for i in loopers)
print(f"max sample frame (J2*735) = {mx}  fits int32 = {mx < 2**31}; max byte offset {mx * 4 + 44}")
print(f"shortest loop region: track {mn[1]} {mn[0]} frames = {mn[0] * SPF} sample frames ({mn[0] * SPF / 44100:.2f} s)")
print("track 14 binary (verify.md row 10): J1 14551 ->", 14551 * SPF, "; analyser 14552 ->", 14552 * SPF)
tot_today = sum(os.path.getsize(os.path.join(REPO, "data-extracted", "sound", "bgm", e["File"])) for e in bgm.values())
tot_b = sum((int(r["J2_frame"]) if r["kind"].startswith("loop") else int(r["end_frame"]) + 1) * SPF * 4 + 44
            for i, r in rows.items() if i in bgm)
print(f"BGM bytes today {tot_today} ({tot_today / 2**20:.1f} MiB); option B {tot_b} ({tot_b / 2**20:.1f} MiB) "
      f"[one-shots kept at end+1; track 44 excluded since X2]")


print("\n== D. own model of FillQueue / WavStreamReader against FakeAudioBackend ==")


class Reader:
    """WavStreamReader: Read() rounds down to whole blocks, returns 0 at end (WavStreamReader.cs:60-88)."""

    def __init__(self, data, block):
        self.data, self.block, self.pos = data, block, 0

    def read(self, count):
        remaining = len(self.data) - self.pos
        if remaining <= 0:
            return b""
        wanted = min(count, remaining)
        wanted -= wanted % self.block
        if wanted <= 0:
            return b""
        out = self.data[self.pos:self.pos + wanted]
        self.pos += wanted
        return out

    def seek_frame(self, frame):  # proposed SeekToSample
        n = len(self.data) // self.block
        if not (0 <= frame < n):
            raise ValueError("ArgumentOutOfRange")
        self.pos = frame * self.block


class Fake:
    def __init__(self, zero_pending=False):
        self.pending, self.bufs, self.zero = 0, [], zero_pending

    def count(self):
        return 0 if self.zero else self.pending

    def submit(self, b):
        if len(b) <= 0:
            return
        self.pending += 1
        self.bufs.append(b)


def fill(reader, fake, looped, loop_start, bufsize, target, bound, state, cap=200000):
    """MusicPlayer.FillQueue, today (bound None, loop_start 0) or proposed."""
    n = 0
    while not state["finishing"] and fake.count() < target and (bound is None or n < bound):
        if len(fake.bufs) > cap:
            return "SPINS"
        b = reader.read(bufsize)
        if len(b) == 0:
            if not looped:
                state["finishing"] = True
                return None
            reader.seek_frame(loop_start)
            b = reader.read(bufsize)
            if len(b) == 0:
                state["finishing"] = True
                return None
        fake.submit(b)
        n += 1
    return None


def ramp(frames=1000):
    return b"".join(struct.pack("<hh", f, 10000 + f) for f in range(frames))


def frames_of(bufs):
    allb = b"".join(bufs)
    return [struct.unpack_from("<hh", allb, i)[0] for i in range(0, len(allb), 4)]


def check_formula(seq, frames, ls):
    for i, v in enumerate(seq):
        exp = i if i < frames else ls + (i - frames) % (frames - ls)
        if v != exp:
            return f"mismatch at {i}: {v} != {exp}"
    return f"formula holds over {len(seq)} frames"


def run(name, looped, ls, bufsize, rounds, consume=3, bound=3, zero=False, frames=1000, data=None):
    data = data if data is not None else ramp(frames)
    nframes = len(data) // 4
    eff = ls if 0 <= ls < nframes else 0  # proposed Play-time fallback
    r, fk, st = Reader(data, 4), Fake(zero), {"finishing": False}
    res = fill(r, fk, looped, eff, bufsize, 3, bound, st)
    sizes = [[len(b) for b in fk.bufs]]
    for _ in range(rounds):
        fk.pending = max(0, fk.pending - consume)
        res = fill(r, fk, looped, eff, bufsize, 3, bound, st) or res
        sizes.append([len(b) for b in fk.bufs])
    seq = frames_of(fk.bufs) if res is None else []
    print(f"{name}: result={res} sizes={sizes[-1] if res is None else 'n/a'} total={sum(len(b) for b in fk.bufs) if res is None else 'n/a'} "
          f"finishing={st['finishing']}" + (f" | {check_formula(seq, nframes, eff)}" if res is None and looped else ""))
    return fk, sizes


run("L1 Play only (16 KiB, ls 600)", True, 600, 16384, 0)
run("L1 + 1 round", True, 600, 16384, 1)
run("L1 today (ls ignored -> 0)", True, 0, 16384, 0)
run("L2 1024-B, 2 rounds", True, 600, 1024, 2)
run("L3 ls 0, 1 round", True, 0, 16384, 1)
run("L4 not looped, ls 600", False, 600, 16384, 0)
run("L5 ls 1000 -> fallback 0", True, 1000, 16384, 0)
run("L5 ls 5000000 -> fallback 0", True, 5_000_000, 16384, 0)
big = bytes(i % 251 for i in range(65536 * 4))
fk, s = run("B1 today: zero pending, not looped, 65536 frames", False, 0, 16384, 0, bound=None, zero=True, data=big)
fk, s = run("B1 proposed bound 3", False, 0, 16384, 1, bound=3, zero=True, data=big)
print("   B1 proposed totals per step:", [sum(x) for x in s])
run("B2 today: zero pending, looped", True, 600, 16384, 0, bound=None, zero=True)
fk, s = run("B2 proposed", True, 600, 16384, 1, bound=3, zero=True)
print("   B2 proposed sizes per step:", s)

# Header with an 18-byte fmt and a 6-byte LIST (WavBuilder.Create(..., formatChunkSize: 18, extraChunk: new byte[6])).
off = 12 + 8 + 18 + 8 + 6 + 8
naive = 44 + 600 * 4
print(f"\nheader fmt18 + LIST6: data offset {off}; naive byte {naive} -> data byte {naive - off} -> frame {(naive - off) / 4}")
r = Reader(ramp(), 4)
r.seek_frame(600)
print("W1: Position after SeekToSample(600) =", r.pos, "; next 16 bytes =", struct.unpack("<8h", r.read(16)), "; Position =", r.pos)

print("\n== E. seam buffers under option B with 16 KiB buffers (4096 stereo frames) ==")
worst = []
for i in sorted(loopers):
    rr = rows[i]
    j1, j2 = int(rr["J1_frame"]) * SPF, int(rr["J2_frame"]) * SPF
    first_tail = j2 % 4096  # last partial buffer of the first pass (reads start at 0)
    steady_tail = (j2 - j1) % 4096  # last partial buffer of every later pass (reads start at J1)
    worst.append((steady_tail, i, first_tail))
worst.sort()
print("smallest steady-state seam buffers (frames):", worst[:6])
print("tracks with steady seam buffer < 1024 frames (23 ms):", [(i, t) for t, i, _ in worst if 0 < t < 1024])
buf_ms = 16384 / (44100 * 4) * 1000
t, i, _ = worst[0]
print(f"track {i}: queue of 3 = [playing, full, {t}-frame tail] -> worst-case margin about {buf_ms + t / 44.1:.1f} ms "
      f"instead of {2 * buf_ms:.1f} ms (target 3)")

print("\n== F. queue margins ==")
for rate in (44100, 22050):
    ms = 16384 / (rate * 4) * 1000
    for tgt in (3, 23, 24):
        print(f"{rate} Hz target {tgt}: best {tgt * ms:.1f} ms worst {(tgt - 1) * ms:.1f} ms, {tgt * 16} KiB")

print("\n== G. demo WAV ==")
print("132300 frames * 4 + 60 =", 132300 * 4 + 60)

print("\n== H. maps sharing a track (loops.tsv used_by_map_entry vs alundra-project/Maps/music-index.json) ==")
mi_path = os.path.join(REPO, "alundra-project", "Maps", "music-index.json")
mi = json.load(open(mi_path, encoding="utf-8"))
print("music-index.json top-level type:", type(mi).__name__, (list(mi)[:5] if isinstance(mi, dict) else mi[:2]))
for idx in (7, 16, 21, 25, 30):
    used = [x for x in rows[idx]["used_by_map_entry"].split(",") if x]
    print(f"track {idx}: {len(used)} maps in loops.tsv")
