# Is the immediate divergence of the loop region [J1,J2) from the true continuation after J2 caused by period jitter,
# or by pass 1's ending tails at the head of the loop region? Compare with a loop region one pass later ([J2,J3)
# continued after J3), from long7/ (renders through J7).
import csv, os
import numpy as np
SPF = 735
S = r"C:\Users\casad\AppData\Local\Temp\claude\D--development-repo-alundra-casaengine-project-converter\b00d1a72-420d-4acc-ab34-0c25fbdad3a1\scratchpad"
L7 = os.path.join(S, 'loop-data', 'long7')
rows = list(csv.DictReader(open(os.path.join(L7, 'events.tsv')), delimiter='\t'))


def first_gt(x, a, b, n, thr=64):
    d = np.abs(np.asarray(x[a:a + n], dtype=np.int32) - np.asarray(x[b:b + n], dtype=np.int32)).max(axis=1)
    o = np.nonzero(d > thr)[0]
    return int(o[0]) if len(o) else -1


out = []
for r in rows:
    if not r['Jumps']:
        continue
    idx = int(r['SoundIndex']); J = [int(v) for v in r['Jumps'].split(',')]
    p = os.path.join(L7, 'bgm_%03d.wav' % idx)
    n = (os.path.getsize(p) - 44) // 4
    x = np.memmap(p, dtype='<i2', mode='r', offset=44, shape=(n, 2))
    per = [J[k + 1] - J[k] for k in range(len(J) - 1)]
    m = min(per[:3]) * SPF
    # B: loop [J1,J2): true continuation at J2 vs loop head at J1
    b = first_gt(x, J[1] * SPF, J[0] * SPF, m)
    # B': loop [J2,J3): true continuation at J3 vs loop head at J2
    b2 = first_gt(x, J[2] * SPF, J[1] * SPF, m)
    lf1 = np.array_equal(np.asarray(x[(J[1] - 1) * SPF:J[1] * SPF]), np.asarray(x[(J[0] - 1) * SPF:J[0] * SPF]))
    lf2 = np.array_equal(np.asarray(x[(J[2] - 1) * SPF:J[2] * SPF]), np.asarray(x[(J[1] - 1) * SPF:J[1] * SPF]))
    out.append((idx, per[0] == per[1], b, b2, lf1, lf2))
    print(idx, 'P2==P3', per[0] == per[1], 'B first>64', b, "B' first>64", b2, 'lastframe it2==it1', lf1, 'it3==it2', lf2, flush=True)
imm_b = [o[0] for o in out if o[2] in (0, 1)]
imm_b2 = [o[0] for o in out if o[3] in (0, 1)]
print('B: diverge within 1 sample on', len(imm_b), imm_b)
print("B' ([J2,J3)): diverge within 1 sample on", len(imm_b2), imm_b2)
print("B' never (whole pass):", [o[0] for o in out if o[3] == -1])
print('last frame it3==it2 on', [o[0] for o in out if o[5]])
