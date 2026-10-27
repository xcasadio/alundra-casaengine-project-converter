"""fixpatch.py - NOT the binary: an in-memory modification of E13 (0x8007F30C) that models the author's answer to Q-H4C2-3 (B):
the first pass of E13 (the impact: target animation 1, flags |0x40 & ~0x30, camera kick) happens only when the rock is on the ground (+0x144 != 0);
in the air the status is put back to 2 (Normal) and nothing else is written. Used only to derive INDICATIVE corrected keys; never a faithful key.

Layout of the patch (the original code is kept for the 'on the ground' case):
  8007F348  j cave                    (was: lui $a0,0x800e)       delay slot 8007F34C 'lw $v1,0x6c($a1)' is unchanged
  cave+00   lw   $t0,0x144($a1)
  cave+04   nop
  cave+08   bne  $t0,$zero,cont
  cave+0C   nop
  cave+10   addiu $t0,$zero,2
  cave+14   sw   $t0,0x10($a1)        (status := 2, Normal)
  cave+18   j    0x8007F368           (epilogue of E13)
  cave+1C   nop
  cave+20 cont: lui $a0,0x800e        (the replaced instruction)
  cave+24   j    0x8007F350
  cave+28   nop
"""
CAVE = 0x801F0000
E13_FIRST = 0x8007F348
E13_AFTER = 0x8007F350
E13_EPILOGUE = 0x8007F368


def j(target):
    return (2 << 26) | ((target >> 2) & 0x3FFFFFF)


def apply(m):
    words = [
        (0x23 << 26) | (5 << 21) | (8 << 16) | 0x144,       # lw   $t0,0x144($a1)
        0,                                                    # nop
        (5 << 26) | (8 << 21) | (0 << 16) | 5,                # bne  $t0,$zero,+5 words (cave+0x20)
        0,                                                    # nop
        (9 << 26) | (0 << 21) | (8 << 16) | 2,                # addiu $t0,$zero,2
        (0x2B << 26) | (5 << 21) | (8 << 16) | 0x10,          # sw   $t0,0x10($a1)
        j(E13_EPILOGUE),                                      # j    epilogue
        0,                                                    # nop
        (0xF << 26) | (0 << 21) | (4 << 16) | 0x800E,         # lui  $a0,0x800e
        j(E13_AFTER),                                         # j    0x8007F350
        0,                                                    # nop
    ]
    for i, w in enumerate(words):
        m.poke(CAVE + 4 * i, w)
    m.poke(E13_FIRST, j(CAVE))
