# D4 and D5: equipment records and 0x8003634C

> Report returned by one agent of the E20.0 workflow of 2026-10-09 (docs/plan-e20-heros.md section 1.2), kept verbatim as evidence.
> Where it disagrees with its check or with the critic, the plan section 0.2 holds the reconciled fact.

## D4 – UpdateItemEffectState (0x800307E8)

**Cadence.**
- FACT: the only caller is `jal 0x800307e8` at 0x80032914 (word 0x0c00c1fa).
- FACT: it runs on every `MovePlayer` tick. The `jr` instructions in 0x80031B50–0x80032914 (0x80031d18, 0x80031dac, 0x80032650, 0x800327e8) are jump-table dispatches, not returns.
- FACT: it is followed by `SetPlayerHpMax` (0x8004dc68) and `SetPlayerHp` (0x8004dd30), fed from hero+0x18 and hero+0x14.

**Fields read.**
- FACT: owned count of item n = `lh 2([[0x8017631C]] + 4n)`, with n < [0x8017638C] (0x8004e428: `84420002`).
- FACT: stats = [0x80176318]. +0xA is WeaponId (`lh`, -1 means none). +0xC is ItemId.
- FACT: item category = `byte[0x800C5F7C + 8n + 6] & 0x7F`, read at 0x800308c4–0x800308cc. I dumped the table from the EXE.
  - Category 1: items 1–10, 14, 16.
  - Category 2: items 0x11–0x14.
  - Category 3: items 0x33–0x38.
  - Every other item, including 0x19–0x1C, is category 0.
- FACT: the slot table `g_itemsProperties` is at 0x800B9FE8, 10 bytes per item: slot, flags, priority, max count, icon.
- FACT: the balance record of item n is entry n+0x1E of BALANCE.BIN (0x800445c0). The offset table is at 0x80143E34 and offsets are relative to 0x80143DF8. The code walks `rec += rec[1]` while `rec[0] < [0x80143DF0]`.
- FACT: [0x80143DF0] is stored at 0x80044524 by `InitializeItems` (0x80044520). Its only caller is 0x8002c3a0, which loads it from `lbu 0xb([0x800E4334])`, so it is the map-info byte +0xB.
- FACT: every equipment record I checked has BalanceLevel 255 and a chain length of 1, from the raw BALANCE.BIN bytes. These were items 1–10, 14, 16, 17–20 and 51–56. The map threshold therefore does not matter for equipment.

**How the three records are filled.** All six pointer and id words are cleared first (0x800308ac–0x800308c0). Record layout at 0x80146E00 is 8 bytes each: pointer, then item id + 0x1E.

| Record | Source | Condition |
|---|---|---|
| [0] 0x80146E00 | scan n = 0x61 down to 0: the highest-numbered owned item (0x800308c4–0x800308e0) | category == 2 |
| [1] 0x80146E08 | `0x8004e030`: `GetItemIdFromSlotId(stats.WeaponId)` via the jump table at 0x800265d4 | id in 1..0x61 and category == 1 |
| [2] 0x80146E10 | `0x8004e0f8`: stats.ItemId, if owned | id in 1..0x61 and category == 3 |

- `GetItemIdFromSlotId` (0x8004e18c) scans items 0..0x7F and keeps the owned item of that slot with the highest priority (field 2). If the item's flag bit 0 is clear, it returns that item immediately. FACT.
- `0x8004e0f8` also has a side effect. It rewrites stats+0xC with the best owned item of the same slot (`sh a1,0xc(v0)` at 0x8004e51c). It returns the old ItemId. FACT.
- Names (INFERENCE: ETC_RES.R is a sparse table, and its order matches the doc's "armor 17, boots 25"):
  - Category 1 are weapons.
  - Category 2 are armors: tissu 0x11, cuir 0x12, ancienne 0x13, argent 0x14.
  - Category 3 are Anneau d'Olga, Anneau d'Oak, Super bracelet, Charme de Nava, Super anneau and Tonique.

**New-game kit** (items 1, 0x11, 0x19, WeaponId 1; ItemId assumed to have no owned item, UNKNOWN):
- Basic sword, item 1 (Poignard): slot 1, category 1, so it fills record [1]. Raw bytes are `ff 11 00 [11 × 0x40] 01 01 01`. There is one attack (attr 1, Power 1) and all 11 responses are 0x40.
  - Attack side: `ComputeNewHp` (0x800446d4–0x8004474c) adds `Power` of `rec+0xF+2·idx`, where idx = anim+1 if anim+1 < count, else 0. With count 1 that is idx 0, so it adds +1 for every animation, but only when attr bit 0x80 is set.
  - Defence side: its responses are 0x40-flagged, so it adds nothing to defence.
- Cloth armor, item 0x11: category 2, so it fills record [0]. Raw bytes are `ff 0f 00 [11 × 0x10] 00`. Count is 0, so it adds 0 power. It becomes the hero's base defence: all 11 responses 0x10, which is ×1.0. Cuir is 0x0c, ancienne 8, argent 4 (raw bytes).
- Normal boots, item 0x19: category 0, so no record gets them. FACT: boots only reach `[0x80127000]`, set by `sw` at 0x8003082c / 0x80030848 / 0x80030864 / 0x8003088c.
  - Value is 3, 2, 1 or 0, from the counts of items 0x1C, 0x1B, 0x1A (calls with a0 = 0x1c / 0x1b / 0x1a). Item 0x19 itself does not count, so a new game gives 0.
  - Readers I found: 0x80031ad0 (`<3` guards hazard tiles), 0x800368bc and 0x800369e0 (`>0`), 0x800374fc (`<2`).
- Record [2] is empty in a new game.
- Other equipment (raw bytes):
  - Poignard +1, Épée +2, Infernale +4, Sacrée +8. The power byte of item 8 is 0x32.
  - Items 5–7, 9, 10, 14 and 16 have count 0, so +0.
  - Anneaux d'Olga and d'Oak (items 0x33 and 0x34) have attack power 3.
  - Super bracelet (0x35) has responses 0x0f.
  - Charme de Nava (0x36) has responses 0x0f, with a 0x80 at response index 6.
  - Super anneau and Tonique (0x37 and 0x38) have [+2] = 0xC8 (200) as a regen period.

**Every tick, after the records:**
1. FACT: copy 0xFF bytes from record [0] to 0x80127008. The 0xFF bytes are 15 chunks of 0x10 plus 0xF, at 0x800309c0–0x80030b3c. If [0] is null, the source is [0x80127EF4], which is hero+0x1C4. It always copies 255 bytes, regardless of RecordSize.
2. FACT: for i = 1..11, rewrite byte `0x80127008+2+i` (0x80030b64–0x80030c18). Let b be that base byte, A the same byte from record [1] and B from record [2]; A and B default to 0x10 when the record is null.
   - If any of b, A, B has `&0xC0 == 0x80`, the result is 0x80.
   - Else if b has `&0xC0 != 0`, the result starts at 0x10. If A also has flags, the result is 0x40 when B has flags too (stored as is), otherwise 0x10.
   - Add `A-0x10` only if A has no flags, and `B-0x10` only if B has no flags.
   - Clamp to 0..0x1F.
   - Example: the new-game kit yields 0x10 for all 11 responses.
3. FACT: regen runs only when `([0x800DC4B8] & 0x7C) == 0`, hero+0x20 is 0, `lhu [0x80126F28]` is 0, hero Hp ≠ 0 and Hp < HpMax (0x80030c24–0x80030c80).
   - For each record i, counter [0x80126FE8+4i] counts up when record i is non-null and its byte +2 is non-zero. It resets and Hp += 1 when `MaxHp < counter` (`slt v0,v0,v1` at 0x80030cd4).
   - Otherwise the counter is set to 0 (0x80030cec).
   - Hp is then clamped to HpMax and stored at hero+0x14.
   - If any gate fails, all three counters are zeroed (0x80030d30–0x80030d3c).
4. The code from 0x80030d40 on is debug printing only (gated by [0x800DC058] < 0). It has no gameplay effect.

**Decompilation differences** (PlayerManager.cs:1138–1340):
- Regen: the C# resets at `MaxHp <= counter`. The binary resets at `MaxHp < counter`, so the period is MaxHp+1 ticks, not MaxHp.
- `EffectiveBalanceStats.CopyFrom` copies the header and 11 responses only. The binary copies 255 bytes, including the attack list.
- `GetItemIdFromSlotId` (reached from this function) scans 100 items in the C#, but 0x80 in the binary (`slti 0x80` at 0x8004e248).
- The response combination, record selection and `[0x80127000]` otherwise match the binary.

## D5 – 0x8003634C

**What it is.** FACT: it is the per-tick driver of an in-progress item-use sequence. It is not a cancel routine.
- `[0x80127164]` holds the item id being used.
- With `[0x80127164] == 0` it returns 1 immediately (0x80036358 → 0x800364b4) and writes nothing.
- Otherwise it switches on `[0x80127164]-0x1F` (valid range 0..0x13) via the table at 0x800235F4. Each handler is called with a0 = the state.

| State (item) | Handler |
|---|---|
| 0x1F (Haricot) | 0x80035204 |
| 0x20 (Cape de sable) | 0x80035260 |
| 0x23 (Bombe) | 0x800352c4 |
| 0x2B (Rouleau terre) | 0x80035320 |
| 0x2C (Livre de terre) | 0x800354d0 |
| 0x2D, 0x2E (Rouleau d'eau, Livre d'eau) | 0x80035790 |
| 0x2F (Rouleau de feu) | 0x80035a84 |
| 0x30 (Livre de feu) | 0x80035c64 |
| 0x31 (Rouleau de vent) | 0x80035eb0 |
| 0x32 (Livre de vent) | 0x80036218 |

- Any other state, such as 0x21, 0x22 or 0x24–0x2A, sets v1 = 1 (0x8003647c).
- After the handler: `[0x8012715C]++` unless it is ≥ 0x7FFFFFFF (0x80036484–0x8003649c). If v1 ≠ 0, `[0x80127164] = 0`. The function returns v1, where 1 means finished and 0 means still running.

**Hurt-path call (0x80031C6C).**
- The two zeroings the discovery attributed to it are done by the hurt path itself, not by 0x8003634C:
  - `sw zero,0x6ffc` at 0x80031c5c.
  - `sw zero,0x7110` at 0x80031c68, in the delay slot of `jal 0x8002f884`, with a0 = 2.
- FACT: `0x8003634C` runs one extra handler tick, using the pre-hurt animation. `sw 0x88` (0x31 or 0x3A) comes later, at 0x80031c84.
- FACT: it is called again at 0x80031CC4 on every normal tick, when there is no damage, Hp ≠ 0 and `(hero+0x188 & 0x80) == 0`. The return value is ignored.
- FACT: the explicit cancel is a separate routine, `0x800350C0`, which runs a per-state cleanup and then zeroes [0x80127164].
  - It is called directly by the death branch (0x800327b4), by the `+0x188 & 0x80` branch (0x80032770), at 0x80032758 and at 0x80031bfc. Those paths cancel outright.
  - The hurt path does not cancel. It only ticks.

**What the handlers cancel** (decoded):
- 0x1F, Haricot: finished if hero+0x2C is null or that entity's +0x68 ≠ 1. If hero+0x18C == 4 it calls cancel and finishes. Otherwise it keeps running.
- 0x23, Bombe: the same, with entity+0x68 == 2.
- 0x20, Cape de sable: finished (no cleanup) if the animation `+0x88` is outside 0x20..0x24. Cancel if hero+0x144 == 0 or +0x18C == 4.
- Cleanup routines (for example 0x80034acc for 0x1F): they clear hero+0x2C, spawn an effect through 0x8003BDD8, and remove the held entity through 0x8003A774.
- 0x2B..0x32: INFERENCE, 13 call sites of the helper `0x800351b8` inside the handlers (0x80035370 to 0x800362d8). The helper returns 0 (keep running) only while the animation is 0x32 or 0x33 and +0x18C ≠ 4. Otherwise it cancels and returns 1.
  - The hero's animation is 0x31 or 0x3A on the tick after a hit, so these sequences end then.
  - Which phases call the helper is UNKNOWN. I did not decode them.
  - The cleanups for 0x2C–0x32 touch entities of the sequence (list at 0x80127FC4, stride 0x294, count [0x80134360]). Their exact effect is UNKNOWN beyond that.
- Meaning of hero+0x18C and hero+0x144: UNKNOWN. The C# calls +0x18C `Slope_18c`.

## What E20.c1 and E20.c2 must port

**E20.c1**
- Equipment power. The three records come from the rules above. Hero damage to a target is `(Power + Σ_i Attacks[idx].Power) · response >> 4` over non-null records [0..2], applied only to attacks with bit 0x80.
- The new-game kit gives +1 from the Poignard and +0 from the armor.
- The exports must carry the item-n records (entry n+0x1E) with their level chain. The attack index rule is `anim+1 < count ? anim+1 : 0`, with the animation latched at 0x80146DF8.

**E20.c2**
- The hero's defence table is the combined 11 responses above, with record [0] as base.
- If no armor is owned, the base is hero+0x1C4. The new game owns armor 0x11.
- Port `UpdateItemEffectState` once per `MovePlayer` tick. It writes the response table, the boots level to [0x80127000], the regen counters and Hp.
- Use the binary's `MaxHp < counter` regen rule, not the C# one.
- Hurt path: zero [0x80126FFC] and [0x80127110], call `UpdatePlayerCarriedEntity(2)`, then call the driver. With no item-use sequence active, the driver is a no-op returning 1. A stub is faithful until E20.h ports the sequences.
- Also port the second driver site at 0x80031CC4.
