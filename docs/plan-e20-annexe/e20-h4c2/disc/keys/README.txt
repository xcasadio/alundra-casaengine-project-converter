h4c2 keys (earth 2, item 0x2C, sprite 17, E13, cancel 0x80034BDC) remade on the REAL code of ALUN_CD.EXE (France, SHA-1 a705f8dab665645af5dfaf8fb348c7926b5caa58)
by copies of the e20-h4 harness (docs/plan-e20-annexe/e20-h4/work/oracle) plus the scripts of ../tools (dropped next to them).  Scenario of all of them: the oracle world
(52 x 60 flat cells, 1248 x 960 px), the hero at (100, 100) px facing right, seed 0x12345678, MP 4/4, the Circle pressed at tick 1, the global sprites 1, 16, 17, 19-24 installed.

Two corrections of the e20-h4 bench, both applied here (see ../report.md section 1.8):
  - slot initialisation of the map load (0x8003B24C-0x8003B2CC: +0 of the 64 slots = the slot index), which the oracle's setup leaves at 0 (option --slotinit, the default of cast_rich.py);
  - the per-frame step of the generator (0x8002BD0C-0x8002BD34: s <- s * 0x7D2B89DD + 0xE06A02E7 at [0x80098708]) added AFTER the passes of every tick (option --pf), the rule of
    docs/plan-e20-heros.md section 1.20 point 3 (suffix _pf).

Files
  cast_2c_pf.json            the whole cast: rows (one per rock: birth, r1, r2, position, status/animation/z/+0xAC/+0x13C/+0x144/flags per tick), kicks (tick, 2), sounds (tick, id),
                             per_tick (rng0 before the passes, rng1 after the passes and before the step, hero animation, item, phase, counter, MP).  Z in raw binary words (DLL = raw - 1).
  targets_2c_pf_inv0.json    six static targets (global sprite 25, ClassA, 5000 HP) at (140,100) (100,140) (180,100) (100,60) (60,100) (140,140): the hits (tick, attacker type, damage, attacker x, y) per target
  targets_2c_pf_inv30.json   the same with a 30-tick invulnerability after each hit (the oracle pokes +0x1CC; the DLL would have to do the same by a test seam)
  cancel_2c_pf_t100.json/.txt, cancel_2c_pf_t60.json/.txt   the hero animation set to 0x31 before the passes of tick 100 / 60: the guard cancels at the driver call; rows from tick T-1 on
  driver_2c_cases.json       609 cases of the driver run on the real code (spawn / effect / sound stubbed and journalled): phase, counter, hero animation, slope, hero X/Y/raw Z, seed,
                             spawn results (ok / nul), terrain given to the newborn (lt: hero Z + 0x1000001, eq: hero Z + 0x1000000, lo: 0), rocks in the world for the cancel -> return value,
                             spawn journal (owner, flag, sprite, x, y, z, direction), generator after, phase/counter/item/hero animation after, status and +0xB8 of the returned entities
  MANIFEST.md5               md5 of every file here (two runs of each script give the same md5)
