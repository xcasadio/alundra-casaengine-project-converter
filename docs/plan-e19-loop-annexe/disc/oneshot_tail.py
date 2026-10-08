"""Read-only. One-shot tracks (no loop marker: 26, 36, 37, 38, 40, 42, 43, 44): per-frame peak/RMS of the last
frames of today's WAV (the extractor stops on the end-of-track frame, Program.cs:301-306, because 0x8008F2E8
rewinds the position in the same frame). A loud last frame means the release that follows the key-off
(0x8008D8A4 -> 0x80093EF4) is cut by the file end."""
import sys, os
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import seam

for n in (26, 36, 37, 38, 40, 42, 43, 44):
    a = seam.read_wav(n)
    nf = len(a) // (2 * 735)
    last = []
    for f in range(nf - 6, nf):
        fr = seam.frame_slice(a, f)
        last.append((f, max(abs(x) for x in fr), round(seam.rms(fr))))
    print(n, "frames", nf, "last frames (frame, peak, rms):", last)
