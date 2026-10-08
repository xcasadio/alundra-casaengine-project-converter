"""Own margin arithmetic: 16 KiB buffers of 44.1 kHz stereo 16-bit = 4096 frames."""
buf = 4096 / 44100 * 1000
for n in (3, 6, 24):
    print(f"depth {n:2d}: full {n*buf:7.1f}  worst {(n-1)*buf:7.1f}  seam134 {(n-2)*buf+134/44.1:7.1f}  seam81 {(n-2)*buf+81/44.1:7.1f}  "
          f"vs 351.5 ms: {(n-2)*buf+81/44.1-351.5:+7.1f}  ring {2*n*16} KiB  queue {n*16} KiB  chunks {2*n}/128")
