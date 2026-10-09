# Oracle attack scenarios (summary; full notes in oracle/notes.md)

> Report returned by one agent of the E20.b discovery workflow of 2026-10-09, kept verbatim as evidence. The plan section 1.4 holds the reconciled facts.

The oracle is extended to the attack scenarios and every scenario ran. Everything below comes from real `ALUN_CD.EXE` code on the D1 oracle, not from embedded expected values.

**Validation**
- **D1 re-run:** with my copy of the tools, S-A and S-B still PASS (216 values each, 0 differences).
- **D1 traces:** `out/d1/S-A.trace.json` and `S-B.trace.json` are byte-equal to the D1 originals.
- **Two interpreters:** all 40 named scenarios give identical results on `emu.py` and `mips.py`. This covers the rows, the hero slot after every tick, the globals and the journal.
- **Mutation tests:** 15 of 15 behave as expected (9 must change the trace, 6 controls must not).
- **Load-delay check:** 0 hazards over the attack runs.
- **Unwritten bytes:** no uninitialised read in any scenario.
- **Determinism:** two full runs give byte-identical `out/`.

**Conventions.** The row of tick N is the state after tick N. PosZ is raw, so rest = terrain + 1 = 1 on the flat field, and the DLL value is raw − 1. PosX and PosY have no offset. The hit pass and the effects are stubs, so no hit is ever delivered.

### Per scenario

(All numbers below are ticks and pad words.)

- **AT1** (Idle, Square at tick 1):
  - The swing starts on tick 1: `+0x88` and `+0x90` are both 0x10 on the press tick, with sound 29.
  - It chains back to 0 on tick 22, so it is on screen for ticks 1..21.
  - Forces and position stay 0.
  - `0x80127110` is set to 0x80028C79 on tick 1 and cleared on tick 2 (type 0). The counter stays 0.
- **AT2** (steady east, Right held):
  - Warm-up is 3 ticks, ending at ForceX 159744.
  - Tick 1 gives 0x10, ForceX 79872, PosX +79872. Ticks 2..21 give ForceX 0, so there is no slide and the total advance is 1.22 px.
  - Tick 22 is Idle. Tick 23 is Moving with ForceX 79872, and tick 24 is back to 159744.
  - Direction stays 24.
- **AT3** (jump, Square at tick 5):
  - Tick 1 is 0x2B and tick 2 is 0x2D. The swing starts on tick 5 as 0x14 with sound 29.
  - It chains to 0x2D on tick 15, so it lasts 10 ticks.
  - The arc is identical to S-A for ticks 1..24 (all forces, positions and IsOnGround).
  - The hero lands on tick 21 and is Idle on tick 22, with sound 11 and 3 dust effects.
  - Added AT3b (Square at tick 14) and AT3c (tick 19), where the swing is still running at the landing. It is cut on tick 22 (0x14→0), the same tick as S-A.
- **AT4:** the attack wins. Tick 1 is 0x10 and ForceZ stays 0 throughout, so no jump.
- **AT5** (Square held on ticks 1..30):
  - One swing only. The record pointer is set on tick 1 only, and the chain back is on tick 22.
  - Nothing happens on the release at tick 31, and the charge timer stays 0.
- **AT6** (extra edges at 4, 10 and 16): all ignored.
  - Sweep with one extra press at tick p:
    - p = 18..22 is ignored. Tick 22 is the chain tick.
    - p = 23, 24 and 25 each start a second swing on that same tick.
  - The AT6 run with a press at tick 23 gives a second swing on 23..43 and chains back on tick 44.
- **AT7** (every cell has ground-property bit 0x20):
  - `+0x180` = 0x2000. `PlayerTryAttack` is entered but the success tail is never reached. There is no sound and no state change.
  - Cell map: only the four footprint-corner cells (columns 3–4, rows 5–6) block, and any one of them is enough. A far cell does not block (AT7c).
  - AT7d (Square + Cross on the gated field) gives a jump, 0x2B on tick 1.
- **AT8** (ledge):
  - Platform at columns 0..4, height byte 1. Start x = 123 px was found by the observed condition in `notes.md`.
  - Tick 1 starts the swing (0x10) and IsOnGround drops to 0, with PosX +1.22 px.
  - Tick 2 turns the swing into 0x2D (ForceZ −32768), and tick 3 into 0x2C.
  - With Right still held the hero lands on tick 10 (sound 11 plus dust) and is Moving, anim 1.
  - Control AT8n (one pixel left): the swing runs ticks 1..21, Idle on 22, Moving on 23.
  - AT8b (harness lowers every cell before tick 7): 0x10→0x2D on tick 8, ForceZ non-zero on ticks 7..13, Idle on landing at tick 15.
  - Walking off a ledge is possible only on tick 1, because the brake leaves a 1.22 px window. AT8b is a field change, not a walk.
- **AT10** (no weapon): item −1, byte `0x80028C13` = 0, record 0, same swing as AT1.
- **AT11:** Left + Square sets `+0x8C` from 24 to 8 on tick 1, before the attack. It stays 8 through tick 22 with Up held, then becomes 16 when Moving starts on tick 23.
- **Extras not asked, informational (X1, X2):** the charge timer starts counting at tick 22 (the swing end) on a chargeable weapon with Square still held.
  - X1: effect 3 on tick 33, sound 42 every 4 ticks, timer 60 on tick 81. The release on tick 91 gives anim 0x18, 16 sparks, sound 43 and a spawn of type 0x1B on tick 112.
  - X2: a release in the air with timer 60 does nothing and the timer is zero on tick 91. This matches D-E20-9.
  - Aura, sparks and sounds are the D1 stubs, so only their calls are journalled.

### Weapon table exercised (AT9)

Slot is the one read from the binary and set via the real `AddItem` and `SetPlayerWeaponId`. `w` is the weapon-record index. Ground and air are the animation ids chosen. "Spawn" is the `SpawnWarpEntity` call at tick (press + delay + 1).

| Weapon | Slot | w | Charge byte | Ground | Air | Sound | Record (type, delay, z) | Spawn tick, ground / air |
|---|---|---|---|---|---|---|---|---|
| Steel Edge | 1 | 1 | 1 | 3F | 41 | 29 | 0, 0, 0 | none |
| Demon Buster | 1 | 2 | 1 | 44 | 46 | 29 | 0, 0, 0 | none |
| Holy Sword Elnard | 1 | 3 | 1 | 49 | 4B | 29 | 0, 0, 0 | none |
| Chain Throw | 3 | 6 | 0 | 12 | 16 | 35 | 0, 0, 0 | none |
| Crusher | 3 | 7 | 1 | 40 | 42 | 35 | 0, 0, 0 | none |
| Hunter's Bow | 2 | 8 | 0 | 13 | 17 | 32 | 3, 8/0, 0x10 | 10 / 6 |
| William's Bow | 2 | 9 | 1 | 13 | 17 | 32 | 4, 8, 0x10 | 10 / 14 |
| Fireball Rod | 5 | 4 | 1 | 11 | 15 | 37 | 0x0B, 10, 0x20 | 12 / 16 |
| Drifting Ice Rod | 4 | 5 | 1 | 11 | 15 | 37 | 0x0E, 10, 0x20 | 12 / 16 |
| Magic staff | 6 | 10 | 0 | none (sound 3) | none (sound 3) | 3 | none | none |
| Item 008 (extra) | 1 | 11 | 0 | 10 | 14 | 29 | 0, 0, 0 | none |

- **Spawn call:** args are owner = hero, table 0, type, x = PosX, y = PosY, z = PosZ + (z << 16) at the spawn tick, direction = {0, 0x10, 8, 0x18}[`+0x98`] = 0x18.
- **Counter and pointer:** for melee the pointer is cleared the tick after the press. For projectile weapons the counter rises by 1 per tick and the pointer is cleared on the spawn tick with the counter equal to the delay.
- **Swing end ticks (ground):** 22 for the swords, 32 for Chain Throw, 35 for Crusher, 24 for the bows, 32 for the rods.
- **Swing end ticks (air):** 15 (back to 0x2D) for the swords, 19 for the bows. Chain Throw, Crusher and the rods are cut at the landing on tick 22.
- **Magic staff:** an air press plays sound 3 and the jump continues untouched.

### Surprising

- **Brake:** a ground swing halves ForceX on the press tick and zeroes it the next tick.
- **Chain-tick press:** a press on the chain tick itself (tick 22) is ignored. The first accepted press is tick 23.
- **Charge timer on a tap:** a tap with a chargeable weapon puts the charge timer at 1 on the press tick, then 0.
- **State flip:** the flip to 0x2D happens the tick after the ground is lost.
- **Air swings:** they do not disturb the jump arc. An air swing running at the landing is cut that tick.
- **Direction:** an attack uses the direction written on the press tick and ignores later direction keys.

### Run command and files

```
python -B docs/plan-e20-annexe/e20-b/oracle/tools/run_e20b.py
```
It takes about 2.5 minutes and exits 0 when everything passes.

I wrote nothing outside `D:/development/repo/alundra-casaengine-project-converter/docs/plan-e20-annexe/e20-b/oracle/`. `CasaEngine.Launcher/Program.cs` in the engine submodule shows as modified in git status; I did not touch it.

Files under that folder:
- `notes.md`: how to run, every setup choice, how WeaponId and items are set (decoded), limits.
- `tools/`: the D1 copy plus `attack.py`, `run_attacks.py`, `mutations_attack.py`, `hazards_attack.py`, `run_e20b.py`.
- `out/summary.txt`: per-scenario key ticks, computed.
- `out/<AT1..AT8n, AT10, AT11, X1, X2>.trace.txt`, `.trace.json`, `.machine.txt`: per-tick traces.
- `out/at9/AT9g-itemNN.*` and `AT9a-itemNN.*`: one ground and one air run per weapon.
- `out/weapons.txt`, `out/at6-sweep.txt`, `out/at7-cellmap.txt`.
- `out/diff2-attack.txt`, `out/mutations.txt`, `out/load-delay-check-attack.txt`.
- `out/d1/`: the D1 re-validation outputs (`comparison.txt` and the rest).
