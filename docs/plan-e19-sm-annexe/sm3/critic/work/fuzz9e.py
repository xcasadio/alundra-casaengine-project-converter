import random
import sys
import models as md
import replay_mips as rp
import gridlib


def main(seed, n):
    rng = random.Random(seed)
    bad = 0
    ones = 0
    for it in range(n):
        x = rng.randint(0, 255)
        y = rng.randint(0, 59)
        w = rng.randint(0, 8)
        h = rng.randint(0, 8)
        mn = rng.randint(0, 10)
        # keep within 3120 cells
        if h > 0 and w > 0 and (y + h - 1) * 52 + x + w - 1 >= 3120:
            continue
        grid = [rng.choice([0, 0, 1, 2, 3, 0x100, 0x102, 4, 6, 8, 0xFF02, 0xFFFD, 0x200, 0xFFFF]) for _ in range(3120)]
        instr = [0x9E, x, y, w, h, mn]
        vec = dict(op='0x9E', instr=['0x%02X' % b for b in instr], ref=0, n_last=0,
                   slots=[dict(status=2, ref_id=-1, flags=0, anim_flags=0, force_z=0, target_anim=0, platform=None, riding=None, xcoll=None, parent=None)],
                   result_before=7)
        v0, res, mem = rp.run(vec, lambda v: grid)
        mdl = md.nine_e(instr, grid)
        ones += mdl
        if v0 != 6 or res != mdl:
            bad += 1
            print("MISMATCH", instr, v0, res, mdl)
    print("9E fuzz", n, "bad", bad, "ones", ones)


main(int(sys.argv[1]), int(sys.argv[2]))
