"""Model of the CURRENT engine streaming path (engine f86a981b) with a loop start, for test values written in advance.

Models (read from the C#, not executed):
- WavStreamReader.Read (WavStreamReader.cs:60-88): returns at most the remaining data, whole blocks, 0 at the end.
- MusicPlayer.FillQueue (MusicPlayer.cs:354-386): bounded to `target` submits per call, stops while pending < target,
  at read 0 a looped track seeks (today: Rewind) and reads again.
- MusicPlayer.Play (MusicPlayer.cs:75-147): inline FillQueue first, then (worker mode) hands the reader to the worker.
- StreamingWorker.Channel.Service (StreamingWorker.cs:224-287): fills ring slots of `slotSize` (= buffer size), ring of
  2 x target slots, same end-of-data rule (today: Rewind).
- MusicPlayer.FillQueueFromRing (MusicPlayer.cs:320-352): at most `target` per call while pending < target.
- FakeAudioBackend (FakeAudioBackend.cs:219-256): submit increments pending; ConsumeBuffers lowers it.

Variant `across=True` is the optional "fill across the seam" rule (each buffer is filled up to its size by continuing
from the loop start), given only to price the alternative.
Pure arithmetic: nothing from the repository is executed.
"""

BLOCK = 4  # stereo 16-bit


class Reader:
    def __init__(self, frames):
        self.frames = frames
        self.pos = 0  # frames read since data start

    def read(self, max_frames):
        remaining = self.frames - self.pos
        if remaining <= 0:
            return None
        n = min(max_frames, remaining)
        if n <= 0:
            return None
        start = self.pos
        self.pos += n
        return [(start, n)]

    def seek(self, frame):
        assert 0 <= frame < self.frames, frame
        self.pos = frame


def read_buffer(reader, looped, loop_start, buf_frames, across):
    """One buffer as a list of (start, count) runs, or None when the track finishes."""
    got = reader.read(buf_frames)
    if got is None:
        if not looped:
            return None
        reader.seek(loop_start)
        got = reader.read(buf_frames)
        if got is None:
            return None
    if not across:
        return got
    runs = list(got)
    filled = sum(c for _, c in runs)
    while looped and filled < buf_frames:
        more = reader.read(buf_frames - filled)
        if more is None:
            reader.seek(loop_start)
            continue
        runs.extend(more)
        filled += sum(c for _, c in more)
    return runs


class Player:
    def __init__(self, frames, looped, loop_start, buf_bytes, target, worker=False, across=False):
        self.reader = Reader(frames)
        self.looped = looped
        # rule: out of range (>= frame count) falls back to 0; negative is clamped to 0 by the asset setter
        self.loop_start = loop_start if 0 <= loop_start < frames else 0
        self.buf_frames = buf_bytes // BLOCK
        self.target = target
        self.worker = worker
        self.across = across
        self.pending = 0
        self.finishing = False
        self.submitted = []
        self.ring = []
        self.producer_done = False

    def fill_inline(self):
        n = 0
        while n < self.target and not self.finishing and self.pending < self.target:
            b = read_buffer(self.reader, self.looped, self.loop_start, self.buf_frames, self.across)
            if b is None:
                self.finishing = True
                return
            self.submitted.append(b)
            self.pending += 1
            n += 1

    def worker_service(self):
        # fills the ring up to 2 x target slots (the worker runs "enough" between two updates)
        while not self.producer_done and len(self.ring) < 2 * self.target:
            b = read_buffer(self.reader, self.looped, self.loop_start, self.buf_frames, self.across)
            if b is None:
                self.producer_done = True
                return
            self.ring.append(b)

    def fill_from_ring(self):
        n = 0
        while n < self.target and not self.finishing and self.pending < self.target:
            done = self.producer_done
            if not self.ring:
                if done:
                    self.finishing = True
                break
            self.submitted.append(self.ring.pop(0))
            self.pending += 1
            n += 1

    def play(self):
        self.fill_inline()
        if self.worker and not self.finishing:
            self.worker_service()

    def update(self):
        if self.worker:
            self.fill_from_ring()
            self.worker_service()
        else:
            self.fill_inline()

    def consume(self, k):
        self.pending = max(0, self.pending - k)


def frames_of(p):
    seq = []
    for b in p.submitted:
        for s, c in b:
            seq.extend(range(s, s + c))
    return seq


def check(p, frames, loop_start):
    seq = frames_of(p)
    period = frames - loop_start
    for i, v in enumerate(seq):
        exp = i if i < frames else loop_start + (i - frames) % period
        if v != exp:
            return f"MISMATCH at {i}: {v} != {exp}"
    return f"f(i) holds over {len(seq)} frames"


def show(title, p, frames=None, loop_start=None):
    sizes = [sum(c for _, c in b) * BLOCK for b in p.submitted]
    spans = ["+".join(f"[{s}..{s + c - 1}]" for s, c in b) for b in p.submitted]
    line = f"{title}\n  n={len(sizes)} bytes={sizes} total={sum(sizes)}\n  frames={spans}"
    if frames is not None:
        line += f"\n  {check(p, frames, loop_start)}; finishing={p.finishing}"
    print(line)


def scenario(label, frames, looped, ls, buf, target, worker, across, rounds):
    p = Player(frames, looped, ls, buf, target, worker=worker, across=across)
    p.play()
    show(f"{label} after Play", p, frames, p.loop_start)
    for r in range(rounds):
        p.consume(target)
        p.update()
        if worker:
            p.update()  # converge: a second Update after the worker refilled
    if rounds:
        show(f"{label} after {rounds} x (ConsumeBuffers({target}) + Update)", p, frames, p.loop_start)
    return p


print("=== A. Rule kept by the recommendation: reads stop at the end of data (no fill across the seam) ===")
for worker in (False, True):
    mode = "worker" if worker else "inline"
    scenario(f"L1[{mode}] R1000 loop 600, 16384 B, target 3", 1000, True, 600, 16384, 3, worker, False, 1)
    scenario(f"L2[{mode}] R1000 loop 600, 1024 B, target 3", 1000, True, 600, 1024, 3, worker, False, 2)
scenario("L3[inline] R1000 loop 0 (default)", 1000, True, 0, 16384, 3, False, False, 1)
scenario("L4[inline] R1000 NOT looped, loop 600", 1000, False, 600, 16384, 3, False, False, 0)
scenario("L5[inline] R1000 loop 1000 (out of range -> 0)", 1000, True, 1000, 16384, 3, False, False, 0)
scenario("L5b[inline] R1000 loop 5000000 (out of range -> 0)", 1000, True, 5_000_000, 16384, 3, False, False, 0)
scenario("L8[inline] R1000 loop 999 (last frame, valid)", 1000, True, 999, 16384, 3, False, False, 1)

print("\n=== B. Same tests at the proposed depth 24 (if the depth change is in the same slice) ===")
scenario("L1@24[inline] R1000 loop 600, 16384 B, target 24", 1000, True, 600, 16384, 24, False, False, 0)

print("\n=== C. Alternative rule priced only: fill across the seam (every buffer full) ===")
scenario("L1x[inline] R1000 loop 600, 16384 B, target 3", 1000, True, 600, 16384, 3, False, True, 0)
scenario("L2x[inline] R1000 loop 600, 1024 B, target 3", 1000, True, 600, 1024, 3, False, True, 2)

print("\n=== D. Existing tests at depth 24 (65536-frame 22050 Hz stereo file = 16 buffers) ===")
p = Player(65536, False, 0, 16384, 24)
p.play()
print(f"Play_StartsAStreamAndQueuesAhead @24, non-looped 65536: pending after Play = {p.pending}, finishing={p.finishing}")
p.consume(2)
p.update()
print(f"Update_TopsTheQueueBackUp @24: after ConsumeBuffers(2) pending = {p.pending} (test asserts 1), after Update = {p.pending} (test asserts 3)")
for sc in (65536, 26 * 4096):
    p = Player(sc, False, 0, 16384, 24)
    p.play()
    a = p.pending
    p.consume(2)
    b = p.pending
    p.update()
    print(f"  with SampleCount {sc}: after Play {a}, after ConsumeBuffers(2) {b}, after Update {p.pending}")
p = Player(65536, True, 0, 16384, 24)
p.play()
print(f"Looped 65536 @24 (worker tests' Play): pending after Play = {p.pending}")

print("\n=== E. Margins (44.1 kHz stereo 16-bit, 16 KiB buffer = 4096 frames) ===")
buf_ms = 4096 / 44100 * 1000
for target in (3, 6, 12, 24):
    best = target * buf_ms
    worst = (target - 1) * buf_ms
    seam = (target - 2) * buf_ms + 134 / 44100 * 1000  # track 6 steady seam buffer, 134 frames (backlog verify seam.py)
    print(f"target {target:2d}: best {best:7.1f} ms, worst {worst:7.1f} ms, at a seam (track 6) {seam:7.1f} ms;"
          f" fill-across seam = worst {worst:7.1f} ms; ring {2 * target * 16} KiB, queue {target * 16} KiB, "
          f"mixer chunks {target * 2}/128")
print("O-E19-63 worst measured: Debug 2-frame top-up gap 351.5 ms, Debug Tick 301.1 + 31 ms frame")
print(f"max target with 16 KiB buffers under SoftwareMixer.VoiceChunkQueueCapacity 128 (2 chunks per buffer): {128 // 2}")

print("\n=== F. Header trap ===")
off = 12 + (8 + 18) + (8 + 6) + 8
naive = 44 + 600 * 4
print(f"fmt 18 + LIST 6: data offset {off}; naive 44+600*4={naive} -> data byte {naive - off} -> frame {(naive - off) // 4}")
off3 = 12 + (8 + 18) + (8 + 4) + 8
print(f"racing_game_music_1.wav fmt 18 + fact 4: data offset {off3} (measured 58 by own parser, 6190583 frames)")

print("\n=== G. Precision trap of a float read ===")
import struct
for v in (5344920, 10695720, 19994940, 16777217):
    f = struct.unpack('<f', struct.pack('<f', float(v)))[0]
    print(f"{v} as float32 -> {int(f)} ({'exact' if int(f) == v else 'LOST'})")
