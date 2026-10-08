"""Read-only: chunk layout and a coarse RMS envelope of a few WAV files (demo content, Alundra BGM)."""
import struct
import sys
import math


def chunks(path):
    with open(path, "rb") as f:
        data = f.read()
    assert data[:4] == b"RIFF" and data[8:12] == b"WAVE", path
    pos = 12
    out = []
    fmt = None
    while pos + 8 <= len(data):
        cid = data[pos:pos + 4].decode("latin1")
        size = struct.unpack_from("<I", data, pos + 4)[0]
        out.append((cid, pos, size))
        if cid == "fmt ":
            fmt = struct.unpack_from("<HHIIHH", data, pos + 8)
        if cid == "data":
            return data, out, fmt, pos + 8, size
        pos += 8 + size + (size & 1)
    raise SystemExit("no data chunk")


for path in sys.argv[1:]:
    data, out, fmt, off, size = chunks(path)
    tag, ch, rate, bps, align, bits = fmt
    frames = size // align
    print(f"{path}\n  chunks {out}\n  fmt tag={tag} ch={ch} rate={rate} align={align} bits={bits}"
          f"  data_offset={off} data_bytes={size} frames={frames} seconds={frames / rate:.3f}")
    # RMS per 0.1 s window, first 2 s and last 1 s, plus global peak
    win = max(1, rate // 10)
    n = frames
    def rms(a, b):
        s = 0
        cnt = 0
        for fr in range(a, min(b, n)):
            for c in range(ch):
                v = struct.unpack_from("<h", data, off + fr * align + 2 * c)[0]
                s += v * v
                cnt += 1
        return math.sqrt(s / cnt) if cnt else 0
    if n < rate * 30:
        env = [round(rms(i, i + win)) for i in range(0, n, win)]
        print("  rms/0.1s:", env)
    else:
        print("  rms first 1 s/0.1:", [round(rms(i, i + win)) for i in range(0, rate, win)])
        print("  rms last 1 s/0.1:", [round(rms(i, i + win)) for i in range(n - rate, n, win)])
        # first and last sample frames
        first = struct.unpack_from("<hh", data, off)
        last = struct.unpack_from("<hh", data, off + (n - 1) * align)
        print("  first frame", first, "last frame", last)
