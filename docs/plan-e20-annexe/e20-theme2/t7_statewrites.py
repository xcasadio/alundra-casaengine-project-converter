import sys
sys.path.insert(0, r'C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/theme2-disc')
import scan, collections
W = scan.W; N = scan.N; T0 = scan.TEXT0
res = collections.defaultdict(list)
for i in range(N):
    w = W[i]
    op = w >> 26
    rs = (w >> 21) & 31; rt = (w >> 16) & 31; imm = w & 0xFFFF
    if op == 9 and rs == 0 and 0 <= imm < 0x50 and imm != 0:  # addiu rt,$zero,K
        for j in range(1, 8):
            if i + j >= N: break
            w2 = W[i + j]
            if (w2 >> 26) == 0x2B and ((w2 >> 16) & 31) == rt and (w2 & 0xFFFF) == 0x88:  # sw rt,0x88(rs)
                res[imm].append(T0 + 4 * (i + j))
                break
            if (w2 >> 26) in (2, 3):  # stop at jal/j
                break
targets = [int(x, 16) for x in sys.argv[1:]]
for k in (targets or sorted(res)):
    sites = res.get(k, [])
    print(hex(k), [(hex(a), hex(scan.func_of(a))) for a in sites][:14])
