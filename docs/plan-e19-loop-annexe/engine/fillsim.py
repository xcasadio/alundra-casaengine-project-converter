"""Model of MusicPlayer.FillQueue (MusicPlayer.cs:273-300) and WavStreamReader.Read (WavStreamReader.cs:60-88),
today and with the proposed loop start + per-call bound, against FakeAudioBackend (FakeAudioBackend.cs:195-228).

Prints the expected submitted buffers (byte sizes and first/last sample frame of each) for the proposed tests,
plus the queue-margin arithmetic of section 4. Pure arithmetic: nothing from the repository is executed.
"""


class Reader:
    def __init__(self, frames, block=4):
        self.frames = frames
        self.block = block
        self.data_len = frames * block
        self.pos = 0  # bytes read since data start

    def read(self, count):
        remaining = self.data_len - self.pos
        if remaining <= 0:
            return None
        wanted = min(count, remaining)
        wanted -= wanted % self.block
        if wanted <= 0:
            return None
        start = self.pos // self.block
        self.pos += wanted
        return (start, wanted // self.block)  # (first frame, frame count)

    def seek_frame(self, frame):
        assert 0 <= frame < self.frames
        self.pos = frame * self.block


class Track:
    def __init__(self, frames, looped, loop_start, bufsize, target, bound, zero_pending=False):
        self.r = Reader(frames)
        self.looped = looped
        self.loop_start = loop_start if 0 <= loop_start < frames else 0
        self.bufsize = bufsize
        self.target = target
        self.bound = bound  # None = today (no bound)
        self.zero_pending = zero_pending
        self.pending = 0
        self.finishing = False
        self.submitted = []

    def pending_count(self):
        return 0 if self.zero_pending else self.pending

    def fill(self, max_iterations=10_000):
        n = 0
        it = 0
        while not self.finishing and self.pending_count() < self.target and (self.bound is None or n < self.bound):
            it += 1
            if it > max_iterations:
                return "SPINS (no bound)"
            got = self.r.read(self.bufsize)
            if got is None:
                if not self.looped:
                    self.finishing = True
                    return None
                self.r.seek_frame(self.loop_start)
                got = self.r.read(self.bufsize)
                if got is None:
                    self.finishing = True
                    return None
            self.submitted.append(got)
            self.pending += 1
            n += 1
        return None

    def consume(self, k):
        self.pending = max(0, self.pending - k)


def show(title, t, note=None):
    sizes = [c * 4 for _, c in t.submitted]
    spans = [f"[{s}..{s + c - 1}]" for s, c in t.submitted]
    print(f"{title}\n  buffers={len(t.submitted)} bytes={sizes} total={sum(sizes)}\n  frames={spans}" + (f"\n  {note}" if note else ""))


def concat_ok(t, frames, loop_start):
    seq = []
    for s, c in t.submitted:
        seq.extend(range(s, s + c))
    period = frames - loop_start
    for i, v in enumerate(seq):
        exp = i if i < frames else loop_start + (i - frames) % period
        if v != exp:
            return f"MISMATCH at {i}: {v} != {exp}"
    return f"concatenation matches f(i) over {len(seq)} frames"


# T-L1: default player (16384 B, target 3), 1000-frame stereo ramp, looped, loop start 600.
for ls, name in ((600, "T-L1 loop start 600"), (0, "T-L3 loop start 0 (default)")):
    t = Track(1000, True, ls, 16384, 3, 3)
    t.fill()
    show(name + " - after Play", t)
    t.consume(3)
    t.fill()
    show(name + " - after ConsumeBuffers(3) + Update", t, concat_ok(t, 1000, ls))

# T-L2: explicit player, 1024 B buffers (256 frames), target 3.
t = Track(1000, True, 600, 1024, 3, 3)
t.fill()
show("T-L2 1024-B buffers - after Play", t)
for k in range(2):
    t.consume(3)
    t.fill()
show("T-L2 - after two ConsumeBuffers(3) + Update", t, concat_ok(t, 1000, 600))

# T-L4: not looped, loop start ignored.
t = Track(1000, False, 600, 16384, 3, 3)
t.fill()
show("T-L4 not looped, loop start 600 - after Play", t, f"finishing={t.finishing}")

# T-L5: loop start out of range (1000 = frame count) falls back to 0.
t = Track(1000, True, 1000, 16384, 3, 3)
t.fill()
show("T-L5 loop start 1000 (out of range) - after Play", t, concat_ok(t, 1000, 0))

# T-B1: bound, fake reports 0 pending, NOT looped, 65536-sample file (16 buffers of 16 KB), today vs proposed.
today = Track(65536, False, 0, 16384, 3, None, zero_pending=True)
today.fill()
show("T-B1 today (no bound) - after Play", today)
prop = Track(65536, False, 0, 16384, 3, 3, zero_pending=True)
prop.fill()
show("T-B1 proposed (bound 3) - after Play", prop)
prop.fill()
show("T-B1 proposed - after one Update", prop)

# T-B2: bound, fake reports 0 pending, looped (today spins forever).
today = Track(1000, True, 600, 16384, 3, None, zero_pending=True)
print("T-B2 today:", today.fill(max_iterations=100_000))
prop = Track(1000, True, 600, 16384, 3, 3, zero_pending=True)
prop.fill()
show("T-B2 proposed - after Play", prop, concat_ok(prop, 1000, 600))
prop.fill()
show("T-B2 proposed - after one Update", prop, concat_ok(prop, 1000, 600))

# Non-44-byte header: data offset 60 (fmt 18 + LIST 6). A naive seek to 44 + 600*4 lands 16 bytes early.
off = 12 + (8 + 18) + (8 + 6) + 8
naive = 44 + 600 * 4
print(f"\nheader fmt18+LIST6: data offset {off}; naive byte {naive} -> data byte {naive - off} -> frame {(naive - off) // 4}")
off2 = 12 + (8 + 18) + (8 + 4) + 8
print(f"racing_game_music_1.wav layout fmt18+fact4: data offset {off2} (wavinfo.out says 58)")

# Section 4: queue margin arithmetic.
print()
for rate in (44100, 22050):
    bps = rate * 4
    buf_ms = 16384 / bps * 1000
    print(f"{rate} Hz stereo 16-bit: {bps} B/s, 16384 B = {16384 // 4} frames = {buf_ms:.2f} ms;"
          f" target 3: best {3 * buf_ms:.1f} ms, worst {2 * buf_ms:.1f} ms")
    for target in (3, 6, 12, 23, 24):
        print(f"   target {target:2d}: best {target * buf_ms:7.1f} ms, worst {(target - 1) * buf_ms:7.1f} ms, "
              f"memory {target * 16} KiB")
# Alundra track sizes after option B (J2 frames x 735 x 4 + 44): largest and examples.
for idx, j2 in ((14, 27204), (7, 21192), (25, 13186), (45, 718)):
    b = j2 * 735 * 4 + 44
    print(f"track {idx}: J2 {j2} -> {b} bytes ({b / 2**20:.1f} MiB) resident if played by PlayClip")
print("LoopStartSample examples: 25 ->", 7272 * 735, " 14 (extractor) ->", 14552 * 735, " 45/46 ->", 380 * 735,
      " max sample offset (J2 track 14) ->", 27204 * 735)
