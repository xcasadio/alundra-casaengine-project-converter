"""Own recomputation of loop regions and seam buffers from the versioned L-b table (read only)."""
import csv, sys
path = sys.argv[1]
BUF = 4096
rows = list(csv.DictReader(open(path, encoding='utf-8'), delimiter='\t'))
out = []
for r in rows:
    if r['kind'] != 'loop':
        continue
    idx = int(r['idx'])
    total = int(r['Frames_new']) * 735
    ls = int(r['LoopStartSample'])
    le = r['LoopEndSample']
    hdr = int(r['Bytes_new']) - total * 4
    region = total - ls
    seam1 = total % BUF          # first seam: the reads started at 0
    seam2 = region % BUF         # every later seam: reads start at the loop start
    out.append((idx, total, ls, le, region, seam1, seam2, hdr))
print("idx total LS LoopEnd region(frames) region_s seam1 seam2 header_bytes")
for o in out:
    print(o[0], o[1], o[2], o[3], o[4], f"{o[4]/44100:.2f}", o[5], o[6], o[7])
print("loop tracks:", len(out))
print("max LS:", max(out, key=lambda o: o[2])[:3])
print("min region:", min(out, key=lambda o: o[4])[:5], f"{min(o[4] for o in out)/44100:.2f} s")
nz = [(o[0], 'seam1', o[5]) for o in out if o[5]] + [(o[0], 'seam2', o[6]) for o in out if o[6]]
nz.sort(key=lambda t: t[2])
print("smallest nonzero seam buffers:", nz[:6])
print("zero seams:", [(o[0], o[5], o[6]) for o in out if o[5] == 0 or o[6] == 0])
print("max total frames:", max(o[1] for o in out), "max LS < 2^24:", max(o[2] for o in out) < 2**24)
