# Completeness critic of E20.0

> Report returned by one agent of the E20.0 workflow of 2026-10-09 (docs/plan-e20-heros.md section 1.2), kept verbatim as evidence.
> Where it disagrees with its check or with the critic, the plan section 0.2 holds the reconciled fact.

**E20.0 completeness critique**

**Verdict:** D1, D2 and D6 meet their acceptance bars. D3, D4, D5, D8 and D9 answer their questions, and their checks narrowed or corrected them. D7 covers the four required items, but its draft E20.a contract is not yet complete (section 5 lists the gaps). Two acceptance items are still open and belong to the main session: updating §0.2, and detailing E20.a and sending it to plan-verifier.

I re-checked these binary facts myself with capstone on ALUN_CD.EXE:
- The RNG state word is at `0x80032988 3c04800a lui a0,0x800a` / `0x8003298C 8c828708 lw v0,-0x78f8(a0)`, which is `0x80098708`. Its image word is `0xB017C93D`; the word at `0x800A8708` is `0x0004FFFF`.
- The frame countdown is `0x80038BE0 2442ffff addiu v0,-1` / `0x80038BE4 bnez` / `0x80038BE8 sw v0,0xa8(s0)`.
- `0x80031CC4 0c00d8d3 jal 0x8003634c` and `0x80032914 0c00c1fa jal 0x800307e8`.
- Hero flags `0x3118C`: the attacker mask computes to `0x801` (it hits classes A and C). As a target, `F & 8` is set, so the hero is class B only.

## 1. Contradictions and which side the binary supports

| # | Statement | Contradicted by | The binary supports |
|---|---|---|---|
| C1 | RNG state at `0x800A8708` (plan §1.2 D1 line 413, D1 `oracle.py:65`, D9's loot-draw description, D2 `hitworld.py` docstring) | Check D1, D2 N1 | **`0x80098708`**. FACT, words above. D1's seed write corrupted sound-table row 48. The real state is consumed at tick 22 of S-A and S-B (landing dust). No compared field depends on it. |
| C2 | D1: "RNG never consumed in S-A/S-B" | Check D1 | It is consumed, 3 draws at tick 22. |
| C3 | D3: "the pass does four things on contact" | Check D3–5, D2 N2 | The check. When the hit is not immune, attr&0xF 6/10 attaches effect 4 and 7/9 attaches effect 5 (`0x800395F4-0x80039664`). The fire bramble's a1 (attr 6) attaches effect 4 to the hero. |
| C4 | D3: "only `0x80031A68` reads the hero's TouchingEntity" | Check D3–5 | The check. Native handlers also read it (`0x800667F8`, `0x80066E58`, `0x8006F8B8`, `0x8007D2B0`). "No HP loss before c2" still stands. |
| C5 | D4: boots stores at `0x8003082C/848/864/88C` only | Check D3–5 | Incomplete. The init routine also stores at `0x80031730` (`sw zero,0x7000`). |
| C6 | D6: "delay d shows for d ticks" | Check D2+D6 | The check, re-verified above. A delay of 0 decrements to −1 and freezes the frame. This does not matter for boxes: the 5 such frames have no record. |
| C7 | D6: the rule `LogicalTick` + last keyframe is "proven at every tick" | Check D2+D6 | Overstated. D6 proved only the mapping from animation-relative tick to box. Two things are unproven: the switch-tick reservation (`AlundraFrameSyncPasses.cs:228-245`), against the binary showing frame 0 on the switch tick (`0x80038BAC`), and restarts on a direction change (`0x80038B08`). |
| C8 | D2 N4: "the only store with displacement 0x1C4 is `0x80039E38`" | Check D2+D6 | Correct reworded as "the only entity-field store". `0x8009784C` is a library register write. |
| C9 | D2 notes: "a zero-size target hits only at d=0" | Check D2+D6 | The check: a zero-size target is a point inside the attacker box. Moot in the data, where sizes are ≥1. |
| C10 | Plan §1.2 D1: "`+0x1C4`, and `0x80127008` for the hero", which treats `0x80127008` as the hero's record pointer | D4, D2 N4 | D4. `0x80127008` is a separate 255-byte table that `UpdateItemEffectState` rebuilds on every MovePlayer tick, from record [0], or from `+0x1C4` if [0] is null. The hit pass reads the hero's `+0x1C4` (record 0, all responses 0, `0x800395D4`). `ComputeNewHp(hero)` reads `0x80127008` (`0x8003A3A0-B4`). D1 open issue 3 is therefore answered: there is no re-pointing. |
| C11 | Plan §0.2.5 and the D5 brief: `0x8003634C` is something the hurt path "cancels" | D5, checked | D5. It is the per-tick driver of an item-use sequence. The hurt path only ticks it. The cancel routine is `0x800350C0`, on other paths. |
| C12 | D1 notes: the effects pass is a stub "with no effect" | Check D1 | The real `0x80038E84` reads and writes hero `+0x1B8..+0x1BB`. S-A and S-B still pass with it. The stub follows the plan, but it breaks the plan's own "real code if it touches the hero's slot" rule. |

## 2. Acceptance items

| Item | Status |
|---|---|
| D1 equal to S-A/S-B field by field | **MET.** 216/216 per scenario, re-run by the check. Caveat: annex B.1 is itself an emulation of the same binary (E19.d2c, ADR-0023). The PASS proves agreement with that earlier emulation, not independence. A bank-layout error common to both runs would cancel out. The trace carries every field the plan asks for (target, current, dir, forces, positions, on_ground, b0, +0x1C, both counters). |
| D2 six cases | **MET.** 6/6 groups and 47 runs, re-run by the check. Expected values are literal. The lists are built by real `0x800384F4`. |
| D6 whole-corpus counts | **MET.** 30585/30585, a per-bank CSV, mutation teeth and the duplicate check. The tick-level claim must be narrowed (C7). |
| D7 covers MovePlayer, counters pass, hit pass and inversion | **MET** on coverage. Contract gaps are in section 5. |
| Facts labelled | **MET.** D3 has one weak spot: "animation 1 = walk" is labelled INFERENCE, which is acceptable. |
| Binary facts re-checked by a second agent | **MET** for D1–D6. **Not met for D7, D8 and D9:** no check report for them is present. These binary claims are unchecked: D7's `UpdateWorld` order (`0x8002E100/108/110`) and the counters loop; D8's tables `0x80022CAC`, `0x80022CEC`, `0x800237F4` and the windows at `0x8003219C`/`0x800321C4`; D9's loot, veto, spawn and handler slots. D9 already shows one error (C1). The D8 tables carry decisions and need a check before E20.d. |
| Completeness critique | This document. |
| §0.2 updated; E20.a detailed and sent to plan-verifier | **NOT MET** (main session). |
| `git status` clean outside `docs/` | **MET with a caveat.** `m CasaEngineMonogame` and `? alundra-datas-analyser` appear. D1 says they predate its work; the author should confirm. |
| Deviations from the plan | D1's tools sit in `d1-oracle-moveplayer/tools/`, not in `docs/plan-e20-annexe/tools/`. `+0x1C4` was not set to `0x80127008`; this was tested as a non-issue, and the premise is wrong anyway (C10). Neither deviation is blocking; the plan text should follow the facts. |

## 3. What §0.2 must now say

**Corrections**
- §1.2 D1 line 413: the RNG state is `0x80098708`, with image value `0xB017C93D`. Fix it in D1 `oracle.py`, D2 `hitworld.py`'s docstring and D9's notes.
- §1.2 D1 line 419: drop "0x80127008 pour le héros" as a pointer (C10).
- §0.2.5, equipment: replace "[décompilation] seulement" with the binary rules from D4:
  - [0] holds the highest owned category-2 armour.
  - [1] holds `GetItemIdFromSlotId(WeaponId)` when its category is 1.
  - [2] holds the owned ItemId when its category is 3. Note the side effect: stats+0xC is rewritten (`0x8004E51C`).
  - The new-game kit gives +1 power from the Poignard (only for attacks with attr&0x80, and no hurting attack in the corpus has that bit) and a 0x10 defence on all 11 responses.
- §0.2.5, `0x8003634C`: replace "rôle inconnu" with C11. It is a no-op returning 1 when `[0x80127164]==0`. It has a second site at `0x80031CC4` on every normal tick.
- §0.2.5, responses:
  - Effects 4 and 5 are attached only when the hit is not `0x80`.
  - A response of 0 is not immunity: it gives a minimum of 1 damage.
  - For the hero, the pass reads `+0x1C4` (record 0, never immune), while `ComputeNewHp` reads `0x80127008`.
  - `0x40` (instant kill) appears 1058 times in the corpus.
- §0.2.5, classes: add that the hero as a target is class B only. Only HitsB attackers reach it, and only the HitsB sprites listed in D3.
- §0.2.3: frame delay 0 freezes the frame (C6).

**New facts**
- MovePlayer's tail calls `UpdateItemEffectState` on every tick (`0x80032914`), then `SetPlayerHpMax` and `SetPlayerHp` from hero +0x18 and +0x14. That call does five things each tick:
  - It writes `0x80127008`.
  - It writes the boots level to `[0x80127000]`. The new game gets 0, because item 0x19 does not count.
  - It handles regen. The rule is `MaxHp < counter`, not the C#'s `<=`. Regen is gated on the pad hold word `[0x80126F28]==0`, among other gates.
  - It copies 255 bytes.
  - It scans 0x80 items, not 100.
- Hurt path: `+0x1CC=0x78` gives 120 ticks of exclusion as a target. That is the real invulnerability; `+0x1D0=0x19` is a 25-tick per-hit block.
- Counters pass `0x80038998`: it covers slots 0 through `[0x80134360]` inclusive with no status filter. It clears `+0x224`, `+0x12C` and `+0x220`. `0x80038634` promotes status-4 slots and writes that count.
- `ComputeNewHp` reads the attacker's `+0x1C8` at call time, tick N+1. A direction change restarts the animation and reloads CurrentAttack (`0x80038AE0-0x80038B10`).
- Spark RNG draws are skipped when the effect pool is full (`0x80039204`).
- `UpdateWorld` order: map events `0x8003C67C`, entities `0x8003B388`, then `0x8003C410` (unidentified). The gate `&0x48` leaves only the lists pass running. *(Needs a check.)*
- D3 map table and corpus totals: 2600 contact-hurting HitsB records on 211 maps. Map 14 gets 16 of 25 hazards, map 15 gets 8. E20.c1 alone makes enemies spark on contact with no HP loss. The 1.1 recette for E20.c1 should say so.
- Pad: the binary routine `0x8002E250` equals `AlundraTickPad.Update`, in 16 bits. The BIOS bit convention and the timing of the pad read relative to `UpdateWorld` are UNKNOWN.
- D6: the export's boxes are exact, so no converter change and no format ADR. The DLL reads boxes through `LogicalClockSprite.LogicalTick`, under the C7 caveat.
- D9: owner options with recommendations (swimming → E20.i; mine cart → not E20; loot and SpawnWarpEntity → a dedicated slice before E20.e; projectiles → split).

## 4. Questions the author must answer before E20.a

1. **Inversion:** fix it under option A (with the measuring spike first), or keep it as a documented deviation? D7 recommends fixing it.
2. **Structure:** option A (a world-driven, tick-major driver) or option C (a fixed 1/50 s step, "M3.a", status UNKNOWN)? Option B fails E20.c1's own 2-tick acceptance, so it cannot stand alone.
3. **RidingEntity:** may the counters pass skip clearing `+0x12C`, as a documented deviation until riding is recomputed per tick?
4. **MovePlayer callees inside E20.a:** port `UpdateItemEffectState` and its HP tail, or stub them? Stubbing would not write HP, boots or `0x80127008`. And is a no-op stub of `0x8003634C` at `0x80031CC4` acceptable?
5. **Effects pass:** the binary runs effects per tick, between physics and hits. Do they stay per frame in the DLL until E20.e?
6. **The pre-existing submodule lines in `git status`:** were they there before E20.0?
7. **D8:** accept the snap rule and ε=1/16 (a tuning choice), pending the E20.d stick probe and a check of D8's tables.
8. **D9:** pick the owner for each item, especially a loot and spawn slice before E20.e.

## 5. Is D7's draft E20.a contract complete?

**No.** These items are missing or under-specified:
- **MovePlayer callees:** it stubs only the three calls and the prologue. It must also decide on `UpdateItemEffectState` with `SetPlayerHpMax`/`SetPlayerHp` (`0x80032914`) and on `0x8003634C` at `0x80031CC4`.
- **Pad timing:** it puts MovePlayer on TickPad words for tick k, but the binary's order of pad read and `UpdateWorld` is UNKNOWN (D1 open issue 7). The DLL currently advances TickPad inside the world loop, after the entities. The contract must fix "TickPad updated before MovePlayer of the same tick" and justify it, or flag it.
- **Gate per tick:** under the mask, MovePlayer and the events must not run while the lists pass still runs (`0x8003B408`). It needs a test.
- **List order:** "hero first in `_entities`" is INFERENCE, unread. Slot order decides "first attacker wins" in E20.c1, so the contract must establish the list order.
- **Placement table:** it omits `0x80038E84` (effects) and `0x8003C410`, and the hit hook is placed "after physics" without the effects pass between them.
- **Animation clock:** the `SyncAnimation` lag (D-E19-13) and the switch-tick reservation (C7) must be stated as constraints passed on to E20.b and E20.c1. Otherwise acceptance cannot catch a one-tick offset of the hit box.
- **Fields the counters pass touches:** `+0x1C` (any DLL readers?) and `+0x1CC` (does opcode `0x86` already write a DLL field?). Neither is inventoried.
- **Stops:** "a bounded number of rig rewrites" needs a number.
- **Acceptance:**
  - Add one D1-oracle comparison of the DLL's per-tick hero trace on S-A/S-B (and the warm-up) against `d1-oracle-moveplayer/out`. That is the purpose D1 was built for, and D7 omits it.
  - SJ6b's 155136 is taken from a test comment ("not re-derived"); re-derive it from the D1 oracle before pinning.
- **Missing prerequisites:** D7, D8 and D9 binary facts need their second-agent check, and the RNG address must be fixed in the D1 tool.

Paths:
- `D:/development/repo/alundra-casaengine-project-converter/docs/plan-e20-heros.md` (lines 413 and 419 to correct)
- `D:/development/repo/alundra-casaengine-project-converter/docs/plan-e20-annexe/d1-oracle-moveplayer/tools/oracle.py` (line 65, RNG poke)
- `D:/development/repo/alundra-casaengine-project-converter/docs/plan-e20-annexe/d2-oracle-coups/tools/hitworld.py` (docstring)
- `D:/development/repo/alundra-casaengine-project-converter/docs/plan-e20-annexe/d6-boites/notes.md` (narrow the claims in C6 and C7)
