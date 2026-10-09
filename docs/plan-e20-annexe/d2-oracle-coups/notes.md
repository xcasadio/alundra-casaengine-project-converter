# E20.0 D2 - the hit-pass oracle (2026-10-09)

Contract: `docs/plan-e20-heros.md` section 1.2, task D2. Ground truth: `ALUN_CD.EXE` (France), text at `0x80020000`, file offset `0x800 + addr - 0x80020000`.
Labels: **FACT** (decoded instruction words or a measurement, with where), **INFERENCE** (with its base), **UNKNOWN**. Nothing outside this folder was written;
`Alundra/`, `Alundra.Tests/`, the converter, the engine, `alundra-project/`, `data-extracted/` and the E19 annexes and `d1-oracle-moveplayer/` are untouched (the D1 emulator
files `emu.py`, `lib.py`, `machine.py`, `bank.py`, `world.py`, `mips.py`, `interp2.py` are copied unchanged into `tools/`; `cmp` equal for `interp2.py`; the others are D1's copies).

## 1. Result

**The six validation cases PASS** (`out/cases.txt`: every run prints inputs, expected result, result got from the real code, verdict):

| # | Case (plan 1.2 D2) | Runs | Verdict |
|---|---|---|---|
| 1 | reordered class mask: HitsClassA-only hits ClassA and not ClassB; HitsClassB-only hits ClassB and not ClassA | 2 | PASS |
| 2 | overlap in raw 16.16, per axis X/Y/Z, (n, m) = (1,1), (5,4), (16,12): `d = (n<<16)-1` hits, `d = n<<16` misses, `d = -((m<<16)-1)` hits, `d = -(m<<16)` misses | 36 | PASS |
| 3 | response `0x80`: no TouchingEntity, `+0x1D0 = 0x19`, HitCounter +1; response `0xC0`: TouchingEntity set, HP unchanged by `ComputeNewHp` | 2 | PASS |
| 4 | minimum damage: `power * r >> 4 = 0` still costs 1 HP (and the HP 1 -> 0 case, and a control with damage 1 by the formula) | 3 | PASS |
| 5 | first attacker: two attackers on one target, only the first of the list touches; the second's HitCounter stays 0 (three slot orders, target between the attackers included) | 3 | PASS |
| 6 | several targets: one attacker on two targets touches both, HitCounter 2 (a third target out of range is untouched) | 1 | PASS |

The expected values are written by hand in `tools/cases.py` from the plan text; nothing is computed by the emulator or by the model. Extra cases (not among the six), all PASS: skip
conditions (target `+0x1D0`, `+0x1CC`, AnimFlags `&0x40`, `+0x20`, status 1/5, attacker without box / attack / attribute / flags), class C and class combinations, effects 4/5 by
attribute, zero sizes, spark centre (positive and negative odd sum), equipment sum, hero sheet (3 runs), response `0x40`, latch of the hero animation (list empty or not), counters pass.

Strength of the evidence:

- **Teeth** (`out/mutations.txt`): 13 one-word patches of the emulated binary (class mask bits, the `+1` of the overlap on each axis and both signs, `+2`, the `0x80` immunity, the minimum 1,
  the `+0x1D0` guard, the target loop, HitCounter, TouchingEntity). Each is caught by the case named in advance; the unmutated baseline has no failing case.
- **Random sets** (`out/random.txt`): 3000 sets from seed `20261009`, each run on the real passes and compared with `tools/model.py` (an independent Python model written from section 0.2.5) on
  TouchingEntity, `+0x1D0`, HitCounter, HP, dead flag, effect journal (ids, targets, spark centres), RNG state after the sparks, latched animation: **0 disagreements**. The sets cover 986 sets
  with a hit, 1363 hits, 525 touches, 838 immune (`0x80`) hits, 24 `0xC0` touches, 20 `0x40` touches, 153 touches with the equipment bit, 49 minimum-damage applications, 164 multi-target attackers, 13
  hero touches (see `out/random.txt` for the full counters).
- **Coverage** (`out/coverage.txt`): every non-debug instruction of `0x80039300`, the spark routine `0x80039188`, `0x8003A374` and `0x8004464C` is executed. The only instructions never executed are
  the `printf`-style branches taken when `[0x800DC058] < 0` (hit pass `0x80039534-0x800395D0`, `ComputeNewHp` `0x8003A3E0-0x8003A524`, `ResolveBalanceTarget` `0x80044660-0x80044678`,
  `0x80044790-0x800447C8`, `0x800447DC-0x80044808`).
- **Two interpreters** (`out/diff2_hits.txt`): 300 of the random sets run on `emu.py` and on `mips.py` (via D1's `interp2.py`); entity slots, lists, latch, RNG word and journal are byte-identical.
- **Unset-byte tracker** (`out/tracker_check.txt`): the entity slots start with an empty "initialised" shadow, so any byte the real code reads that the harness did not write is reported. All runs report none;
  `tools/tracker_check.py` shows the tracker is live (leaving `+0x1D0` unwritten is reported at `0x8003941C`).
- Deterministic: two complete runs give identical `cases.txt`, `random.txt`, `mutations.txt`, `diff2_hits.txt`.

## 2. How to run

```
python -B docs/plan-e20-annexe/d2-oracle-coups/tools/run_all.py          # about 25 s; rebuilds out/; exit code 0 = six cases + random + diff2 + mutations all fine
python -B .../tools/cases.py out        # the six cases and the extras only
python -B .../tools/random_sets.py out 3000 20261009
python -B .../tools/mutations.py out ; diff2_hits.py out 300 ; tracker_check.py ; dump_dis.py out ; scan_fields.py ; scan_sheet.py
```
Needs Python 3 with `capstone`, the disc file `D:/development/repo/Alundra Remake/Alundra (France)/Alundra (France)_extracted/ALUN_CD.EXE` and `DATA/BALANCE.BIN`
(used only to check the encoding, section 4), and `alundra-project/Data/balance.json`.

## 3. What is real code and what is stubbed

| Piece | Run as |
|---|---|
| `0x80038634` slot pass (sets `[0x80134360]`), `0x800384F4` list pass (active list `0x80127B28`, count `0x80127D28`) | real code |
| `0x80039300` hit pass (`UpdateBalanceRecords`), its spark routine `0x80039188` incl. the pseudo-random draws | real code |
| `0x8003A374` `ComputeNewHp`, `0x8004464C` `ResolveBalanceTarget` | real code. The harness calls `ComputeNewHp` for every entity left with a TouchingEntity, in slot order, as the D handlers do (the dispatch of the D programs is not modelled) |
| `0x80044520(level)`, `0x80044550(sprite index)` | real code (random sets: level D and sprite index drawn at random) |
| `0x80038998` counters pass | real code (extra case only) |
| `0x8003BDD8` (spark effect), `0x8003BE74` (effect attached to a target) | stubs from `machine.py`: note id and arguments, return a zeroed dummy slot `0x801E8000` (non-zero, so the spark RNG draws happen) |
| `0x800490FC` sound | stub from `machine.py`, not reached |
| `0x80081604`, `0x800815EC` (the printf-style calls at the end of `ComputeNewHp`, `0x8003A550-0x8003A56C`) | real code, which is `03e00008 jr $ra; nop` in this build (FACT, `0x800815EC` and `0x80081604`) |
| `0x80038E84` effects pass | stub, never called here |

Order per set: restore -> write entities -> `0x80038634` -> `0x800384F4` -> `0x80039300` -> `ComputeNewHp` for each touched entity (-> `0x80038998` in the extra case).
The tick order `0x8003B388` is in the D1 notes; the other passes (events, animation, physics) are not part of D2.

## 4. Memory layout the harness builds

| Address | Content |
|---|---|
| `0x80143DF8` | balance blob = `balance_enc.build_blob()` (512 u16 offsets at +0, records at the offsets of the JSON `FileOffset`), 11169 bytes. Base read by `0x80044550` |
| `0x80143DF0` | balance level D (`0x80044520`: also zeroes the three equipment sheet pointers `0x80146E00 + 8k`) |
| `0x80146DF8` | latched hero animation (written by the pass at `0x80039348`) |
| `0x80146E00`, `+8`, `+0x10` | equipment sheet pointers (random sets: 0 or a corpus record) |
| `0x80127D30 + 0x294 * i`, 64 slots | entities. Only the fields of `hitworld.FIELDS` are written: `+0x10` status, `+0x14` HP, `+0x1C`, `+0x20`, `+0x28`, `+0x6C` flags, `+0x88` (hero), `+0xB4` AnimFlags, `+0x1C4` sheet, `+0x1C8` attack pointer, `+0x1CC`, `+0x1D0`, `+0x1D4` box pointer, `+0x1D8..0x1E0` ModdedPos, `+0x1F0..0x1F8` target sizes, `+0x1FC..0x204` HitBox, `+0x214..0x21C` attacker sizes, `+0x220` HitCounter, `+0x224` TouchingEntity |
| `0x80127008` | hero working sheet (<= 0xFF bytes), read by `ComputeNewHp` for the entity at `0x80127D30` |
| `0x801E6000` | synthetic attacks (2 bytes: attribute byte, power byte) |
| `0x801E4000` | sheets that are corpus records with stated changes (`cases.Scratch`) |
| `0x801E7000` | box record (only its address is used: `+0x1D4` is tested for non-zero) |
| `0x80098708` | pseudo-random state, seeded `0x12345678` (cases) or per set (see section 7, N1) |
| `0x800DC058` | 0 (>= 0: the printf-style branches are skipped) |

Size units: raw 16.16. A box size of n px is stored `(n << 16) - 1` (FACT, attacker: `0x80038C78-0x80038C88`; target: `0x80039CB8-0x80039CC0`, with n = 0 stored 0 for the target and -1 for the attacker, see N3).

### Balance-record encoding and its source

Source: `alundra-project/Data/balance.json` (`EntriesBySpriteIndex`, 512 entries, `NextLevel` chain: 613 records). `tools/balance_enc.py` encodes a record to bytes:

| Byte | Field |
|---|---|
| +0 | `BalanceLevel` |
| +1 | `RecordSize` (`0x80044550` steps with `addu a0,a0,[a0+1]`) |
| +2 | `MaxHp` (`InitializeEntity` `0x80039E3C`) |
| +3..+13 | `DamageResponses[0..10]`: the response of attribute k is the byte at record + k + 2 (`ResolveBalanceTarget` `lbu 2(record + (attr & 0xF))`) |
| +14 | `AttackCount` |
| +15 + 2k | attack k: byte 0 = `AttackAttribute` (low nibble = `AttackType`, bit 7 = `UsesEquipmentPower`), byte 1 = `Power` |

The encoder refuses a JSON attack whose `AttackType` or `UsesEquipmentPower` disagrees with `AttackAttribute`. **Check: the blob built from the JSON equals the disc file `DATA/BALANCE.BIN` byte for byte**
(613 records, 0 differing, 512 table entries equal, both 11169 bytes: `out/balance-encoding.txt`). `Attacks[k]` of a sheet is at `sheet + 0xF + 2k`; `CurrentAttack` (`+0x1C8`) points at one such entry.
The hand-built cases use corpus records picked by property (first match, deterministic): sword attack = attribute 1 power 3, neutral target = response 16 for attribute 1 and MaxHp >= 12, immune = response `0x80`,
kill = `0x40`, 50 % = response 8, power-1 attack. No corpus record has a `0xC0` response: that case uses a copy of a corpus record with `DamageResponses[0] = 0xC0` (written through the same encoder, stated in the case text).

## 5. Facts decoded for D2 (all in `out/dis_hits.txt`, address and instruction word)

- Hit pass `0x80039300`: latches `[0x80127DB8]` (hero TargetAnimationId) into `[0x80146DF8]` in the delay slot of `blez v1,0x80039778` (`0x80039344/0x80039348`, `1860010c`, `ac446df8`), so the latch happens even with an empty list.
  Attacker loop `0x80039350..0x80039770`, target loop `0x800393FC..0x80039758`, both over the list `0x80127B28`.
  Attacker needs `+0x1D4 != 0` (`0x8003936C`), `+0x1C8 != 0` (`0x8003937C`), the first byte of the attack `!= 0` (`0x8003938C`), and a non-zero class mask (`0x800393C0`, mask `0x8003939C..0x800393BC`).
  Target skipped for the same index (`0x800393FC`), `+0x1D0 != 0` (`0x80039424`), `+0x1CC != 0` (`0x80039434`), `+0xB4 & 0x40` (`0x80039448`), `Flags & mask == 0` (`0x8003945C`).
  Overlap `0x80039464..0x8003951C` (X, Y, Z in the order `+0x1D8/+0x1F0/+0x214`, `+0x1DC/+0x1F4/+0x218`, `+0x1E0/+0x1F8/+0x21C`).
  Response `lbu 2(sheet + nibble)` with the sheet from the target's `+0x1C4` (`0x800395D4..0x800395E0`); `(r & 0xC0) == 0x80` jumps to `0x8003966C` (skips effects and TouchingEntity). Effects: nibble 6 or 10 -> `0x8003BE74(0,4,0,target)`, nibble 7 or 9 -> `(0,5,0,target)` (`0x800395F0..0x80039664`).
  `0x80039668 sw s1,0x224(s0)`; `0x80039670 sw 0x19,0x1d0(s0)`; `0x80039680 sw ,0x220(s1)`; spark centre `0x80039684..0x80039744` then `jal 0x80039188`.
- Spark routine: 3 iterations of `0x8003BDD8(0, 9, 0, cx, cy, cz)`, 3 LCG steps each (`x' = x * 0x7D2B89DD + 0xE06A02E7`), the draws only if the effect slot is non-zero (`0x80039204`).
- `ComputeNewHp(e)`: attack = `[e.+0x224].+0x1C8`; sheet = `0x80127008` if `e == 0x80127D30` else `e.+0x1C4` (`0x8003A3A0..0x8003A3B4`); `e.+0x14 = ResolveBalanceTarget(attack, sheet, e.+0x14)` (`0x8003A528`, `0x8003A3E4..`); returns `HP == 0` (`0x8003A574`).
- `ResolveBalanceTarget(attack, sheet, hp)`: returns `hp` if `sheet == 0` (`0x8004467C`), `attack == 0` (`0x80044684`) or attribute byte 0 (`0x80044694`); `r = sheet[(attr & 0xF) + 2]`; `r & 0xC0 == 0x40` -> 0 (`0x800446B4 -> 0x800447CC`); other non-zero top bits -> `hp` (`0x800446BC`);
  otherwise power `+= Attacks[g+1].Power` of each non-null equipment sheet at `0x80146E00 + 8k` when `attr & 0x80` (count 0 -> nothing, `g+1 < count` -> `sheet + 0xF + 2(g+1)`, else `Attacks[0]`); `dmg = (power * r) >> 4`, 0 -> 1 (`0x80044750..0x80044764`); `hp = dmg < hp ? hp - dmg : 0`.

## 6. Corpus facts found while building the cases (`alundra-project/Data/balance.json`)

- Response bytes present in the 613 records (counts): 0 (23), 1 (45), 2 (1), 4 (11), 5 (1), 6 (38), 8 (28), 12 (32), 15 (21), 16 (1321), 31 (2), 32 (1), 64 (1058), 128 (4161). **No `0xC0`.**
- Attack attribute bytes present: 0, 1, 2, 3, 4, 6, 7, 8, 9, 10, 11, 129, 130, 131, 133, 134, 135. No attack has a non-zero attribute byte with a zero low nibble, so the "non-zero byte, nibble 0" path (response read from `sheet[2] = MaxHp`) cannot occur in the data.

## 7. Differences found against the plan and against D1

- **N1 (D1 and plan 1.2 D1)**: the pseudo-random state word is **`0x80098708`**, not `0x800A8708`. FACT: `lui a0,0x800a; lw v0,-0x78f8(a0)` (`0x80032988/0x8003298C`, and `0x800391B0`/`0x8003920C` in the spark routine) = `0x800A0000 - 0x78F8`. The EXE image holds `0xB017C93D` there at load (so the game's initial state is that value unless reseeded; INFERENCE: nothing reseeds before the first draw, not checked).
  D1's `oracle.py:65` wrote its seed to `0x800A8708`, which in the image holds the unrelated data word `0x0004FFFF`. S-A/S-B never draw random numbers and the comparison passed, so D1's result is unaffected, but D1's "seed noted" and the plan sentence name the wrong address. D2 seeds `0x80098708`.
- **N2 (section 0.2.5, hits)**: the effects 4 and 5 are attached only when the response is not `0x80` (they are before the TouchingEntity store, after the immunity branch); an immune hit attaches nothing and still sets `+0x1D0 = 0x19`, increments HitCounter and creates the 3 sparks. Attribute 7 gives effect 5 only (not 4 then 5). The plan lists them without this condition.
- **N3 (section 0.2.5 / 1.2, sizes)**: "a size of n px is stored `(n << 16) - 1`" holds for n >= 1. For n = 0, a **target** size is stored **0** (`0x80039CC8`, `0x80039CE0`, `0x80039CF8`, `beqz ... sw zero`) and an **attacker frame** size is stored **-1** (`0x80038C80-0x80038C88`: `sll 16; addiu -1`, no zero test). Consequence tested in `x4`: a zero-size target hits only at `d = 0`; a zero-size attacker box reaches only `d >= 0` inside the target.
- **N4 (hero, for E20.c1)**: the hit pass tests the response of the target in the target's own `+0x1C4` record (`0x800395D4`), the hero included; `ComputeNewHp(hero)` reads the working sheet `0x80127008` (`0x8003A3B0`). The two can differ (case `x7`: touched with a neutral `+0x1C4` and an immune `0x80127008` -> touched, no damage; the reverse -> not touched).
  FACT: the only store with displacement `0x1C4` in the code is `0x80039E38` (`InitializeEntity`, result of `0x80044550`). `0x80127008` is filled by `0x800307E8` (a 0xFF-byte copy of the sheet pointed at by `[0x80146E00]`, decoded from `0x800309A8`; the rest is D4). INFERENCE: the hero's `+0x1C4` is never re-pointed (a scan by displacement misses stores through a biased base register such as the counters pass's `base + 0x1D0`; D1 measured `+0x1C4` unchanged after real `0x800307E8` runs in S-A/S-B). Answers D1's open issue 3 as far as this evidence goes.
- **N5**: `0xC0` is reachable in the code (TouchingEntity set, HP unchanged) but no record of `balance.json` has it; it can only appear through a sheet that is not in the file (the hero's merged sheet) - UNKNOWN whether any equipment produces it (D4).
- Everything else in section 0.2.5 on the hit pass, the damage formula, the latch, the first-attacker rule, multi-target hits, the counters pass and the reordered mask was reproduced exactly (cases 1-6 and extras). The 37-callers claim and the D-handler dispatch are not part of this oracle.

## 8. Limits and open issues

1. The entity sets are synthetic: positions, sizes and flags are drawn or hand-written, not read from a running game; the boxes are not produced by `0x80038AB4` from sprite frames (D6), `HitBox` is written directly.
2. The oracle does not run the D-handler dispatch (`0x800386D0`); calling `ComputeNewHp` for each touched entity in slot order stands for the D handlers. In the real game the order and the tick of that call (tick N+1, after `MovePlayer`) matter for the latched animation (the latch is tested: `x6`, `x9`, `x10`).
3. `0x8003BDD8`/`0x8003BE74` are stubs: the effect objects themselves, and the sparks' final positions/velocities written into the slot, are not modelled; only ids, targets and the spark centres (journal arguments) and the RNG advance are checked.
4. The pad, physics and animation passes are not run: D2 does not claim that hits happen at the tick the DLL will compute; that is E20.a/c1 and D7.
5. The debug branches (`[0x800DC058] < 0`) are unexercised by design.
