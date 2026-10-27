# E20.i2a T0 measures (2026-10-26, HEAD 00516f6d), STOPPED at T0 (c)
Files in this directory: temp-wiring.patch.txt (temporary wiring, not committed), I2aT0TempProbe.cs.txt (probe test), runs/wired-dump.txt, runs/unwired-dump.txt, runs/wired-full.txt, runs-Release/p1..p5.{log,trx}, replay/ (copy of e20-d oracle replayed), probes/ (the lecture's probes replayed).
b) replay: sscen W1 W2 W5 W10 -> out-swim/w1,w2,w5,w10 identical to repo (diff -rq empty); rscen G9 -> out/g9 and summary-G9 identical; probes s4,s5,s6,s7,s8,s9,s2,s1 outputs identical (s3 differs only by the arguments given).
c) wired gap per trace (tol = raw units on pos_x/pos_y; shift = pad shifted from dllBounce-1 by 1):
 R9-deepwater-noboots: no shift 76 diffs (force_adj tick 85 vs 86, bounce 0x3E dll 86 / oracle 87); SHIFT 85 by 1: tol 0 -> 1 diff (pos_x tick 84, 1 raw unit), tol 1 -> 0.  => t1
 W1-run-L0, W1-run-L1: identical to R9 (dll bounce 86, oracle 87; shift 85 by 1, tol 1 -> 0 diffs)  => t1
 W1-walk-L0/L1, W1-jump-L0/L1, W1-fall-walk-L0/L1, W2-L0/L1-inside-R, W5-L0/L1-wall-walk, W10-shallow-to-deep-L0: 0 diffs at tol 0 (no tolerance at all)
 W5-L0-wall-diag: tol 0: 97, tol 1/16/7424: 96 diffs. pos_x equal (wall at 21299200 rel.), pos_y: oracle steps 1424 then 1 raw unit at ticks 45 and 46 (binary scales both axes by the same halving share, then X moves its last unit), DLL steps 75296 at both: constant offset from tick 46 = 149167 raw units (2.276 px), 73872 at tick 45. 7424 of D-I2A-6 t2 is exceeded 20 times. => STOP
 unwired (HEAD) gap: all 15 traces differ (195, 502, 502, 296, 296, 416, 416, 732, 732, 112, 112, 502, 502, 610, 410 diffs at tol 16).
c) suite Release wired (skipcopy, --no-build, --blame-hang-timeout 60s): p1 Ai. 1241/0 red; p2 Hero.Hero* 1299/1 red; p3 Hero. apart 550/0; p4 Arcs.Audio.Cameras.Cells.Combat.Dialogues.Entities.Events. 2208/0; p5 rest 2191/1 red; total 7489, 2 red:
  Alundra.Tests.Hero.HeroSwimArenaTests.WithoutTheBoots_TheHeroSwimsToo_AnAcceptedDeviation_O_E19_22 (predicted: inverts)
  Alundra.Tests.Support.PrefabArcSupportTests.PrefabMode_OnMap478_TheHeroIsTheRealPrefabOfTheHero (NOT predicted: line 126 pins the live hero mask to 0x41, the hero ticks of the first frame make it 0x10041)
 All boots tests green (HeroSwimTests W/R9 boots, b1b/b1c, 40 water files, story chains 389-392/179/intro).
d) pseudo-bit: CellsCollisionField.DeepWaterWallMaskBit = 0x10000 (bit 16; tileFlags = walkability | gp<<8 < 0x10000, WalkabilityMaskFor <= 0x1041); no existing member of that name (rg); check_names.py --rules 0 findings 1 warning before (no new type needed).
e) preset: mur-m14 = new Preset("mur-m14","test-mur-m14",14,40,48,1,[],[],[],[]) (kit only, boots level 0); (40,48) height 1 non-water, (40,49) deep (out_c9_map14_grid); table 34 -> 35 (TheThirtyFourPresets_ test + AllPresetNames line; EveryPresetName and TheOtherPresets pick it up).
