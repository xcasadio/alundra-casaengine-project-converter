World keys of E20.h1 (the five instant items) on the R-EL-4 bench, remade with the slot index written (+0 = i, +0x48 = -1; R-EL-1) and the frame-end step of the generator ("_pf").
Produced by ../keys_h1.py on the emulated binary (real MovePlayer, TryUseItem, start dispatcher, start and heal functions, UseItem, the real passes); two runs give identical manifests
(manifest.txt, manifest_run2.txt, ../out/keys_compare_out.txt).

Bench: the hero at (100, 100) px (6553600, 6553600 in 16.16) facing east (direction 24) on the flat synthetic field, binary Z convention (rest = terrain + 1, so PosZ = 1 and the DLL's PosZ + 1 = 1 with the
DLL's PosZ = 0), seed 0x12345678 posed before tick 1, Circle pressed at tick 1 (and at the ticks of "presses"), ONE tick per frame.  The item is given by the real AddOneItemIfUnlocked (so the count
is capped by the item's maximum: Herb 9, the four others 1), equipped (stats + 0xC), the hero entity's life is LOWERED first (+0x14 = hp, +0x18 = hpmax when "hpmax" is given), the magic is set in
the stats.  The effect pool is the stub of the bench: every creation is obtained unless "free_slots" says that only that many creations succeed in the whole run (a full pool: the real function
then answers 0).

A key file: {"label", "item", "args": {...}, "pf": true, "uninitialised_reads": [], "rows": [one row per tick]}.  A row:
  t, hero_hp, hero_hpmax (entity +0x14, +0x18), stats_hp, stats_hpmax, stats_mp, stats_mpmax (the mirror: stats written at the end of MovePlayer), count (the item's counter), in_use / phase / counter
  (the three item words: 0 for an instant item), hero_anim_t / hero_anim_c (the hero's animation, never touched by an instant item), pos (hero X, Y, Z in the binary's frame),
  rng0 (the generator at the start of the tick), rng1 (after the draws of the tick, BEFORE the frame-end step), rng2 (= rng1 stepped once: what the next tick starts with, "_pf"),
  draws (the number of draws between rng0 and rng1), events (the stub calls of the tick in the order of the code: ["sound", id] and ["effect", [a0, effect id, animation, x, y, z]] with a trailing
  "refused" for a creation the pool refused), effects (for each creation of the tick, in order: obtained, args, and the fields the code left: x3c/y40/z44 = position, fx54/fy58/fz5c = forces).
A DLL test reads: the life and magic after the frame, the count, the sounds (the alarm 400 = 0x190 can come first: HpMax >= 5 Hp, E20.c2a), the created slots (identifier, animation, X, Y, Z, forces; the
position of the binary's Z is the DLL's PosZ + 1), and the generator AFTER the frame equals rng0 of the next tick; the number of steps the DLL makes during the frame is draws + 1.
