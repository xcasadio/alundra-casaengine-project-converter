# Adversarial check of the E20.c1 discovery

> Report returned by one agent of the E20.c1 discovery workflow of 2026-10-09, kept verbatim as evidence. The plan holds the reconciled facts.

# Adversarial check of E20.c1 discovery (reports A and B)

The binary was re-decoded with capstone (`ALUN_CD.EXE`, France). The c1 oracle was re-run (`run_c1.py`): exit 0, every verdict PASS, and the new `out/manifest.txt` is byte-identical to the previous one (diff rc 0). No repo file was created or changed. Scratch scripts live only in the session scratchpad.

## (1) Report B: decision-carrying binary facts

| Claim | Verdict | Evidence | Correction |
|---|---|---|---|
| Hit list: status−2 <u 2 and `+0x20==0`, slot order, hero first | CONFIRMED | FACT `0x800384F4`: `a0=0x80127D30` (slot 0), `0x80038550 addiu -2; sltiu 2`, `0x80038560 lw -0x1ac(+0x1cc)=+0x20; bnez skip` | — |
| Order: programs → counters → list → clock → physics → hit | PARTLY REFUTED | FACT `0x8003B3A0..3F0`: jal 38634, 386D0, 38998, 384F4, 38E18, 38364, **38E84**, 39300 | `UpdateActiveEffects 0x80038E84` sits between physics and the hit pass. The DLL hook (`AlundraWorldProxy.cs:2686-2692`) omits it. Effect updates (`0x8003C410`, called at `0x8002E110`) come after the entity block, so B's "sparks updated in tick N" holds. |
| Latch `0x80146DF8` written even when the list is empty | CONFIRMED | FACT `0x80039344 blez v1` with delay slot `0x80039348 sw a0,0x6df8` and `a0=[0x80127DB8]`. That address is hero `+0x88` (TargetAnimationId), not the current animation. | — |
| Attacker filters: `+0x1D4`, `+0x1C8`, attr byte ≠0, class mask | CONFIRMED | FACT `0x80039364-0x800393C0`. Mask: `(F>>2)&1 \| (F<<2)&8 \| (F&0x1000?0x800:0)`; a zero mask skips. | — |
| Target filters `+0x1D0`, `+0x1CC`, `B4&0x40`, Flags&mask; body = `+0x1D8`/`+0x1F0` | CONFIRMED | FACT `0x8003941C/942C/9444/9458`, overlap `0x80039464-0x8003951C` | — |
| What a hit writes | CONFIRMED but incomplete in B §6 | FACT `0x800395E8 andi 0xc0; beq 0x80 → 0x8003966C`. That jump skips effects 4/5 **and** `0x80039668 sw s1,0x224`. `+0x1D0=0x19`, attacker `HitCounter++` and the sparks still run. | When the response has `&0xC0==0x80`, the hit sets no `TouchingEntity` and so no D program. Plan 0.2.5 already says this; B's "a hit changes TouchingEntity" must be conditional. |
| Effects 4 (nibbles 6, 10) and 5 (nibbles 7, 9) via `0x8003BE74(0,k,0,target,1,0,0,0)` | CONFIRMED | FACT `0x800395F4-0x80039664` | — |
| `CurrentAttack` reload in the switch block | CONFIRMED | FACT `0x80038AD8 a2=+0x88`; `0x80038B74 lbu 0xe(a1)`; `a2+1<count → rec+2a2+0x11`, else `rec+0xF`, count 0 → 0; `sw 0x1c8` at `0x80038BAC` (only direct writer) | — |
| Chain end re-enters the switch block | CONFIRMED | FACT `0x80038D60 sw a2,0x88`; `j 0x80038b18` | It also writes `+0x88`. |
| Box loading: signed offsets <<16, sizes (n<<16)−1, `0xFFFF` gives `+0x1D4=0` | CONFIRMED | FACT `0x80038C2C-0x80038CB8` | On `0xFFFF`, `+0x208..+0x21C` keep their previous values. |
| Spawn: `+0x1C4`, Hp, HpMax, UpdateAnimation, HitBox | CONFIRMED | FACT `0x80039E30-4C`, `0x80039EA8`, `0x80039F30`. Body sizes are 0 when n=0 (`0x80039CC8`). | — |
| HitBox = Pos + CollisionOffset, only when there is a box | CONFIRMED | FACT `0x80038074-0x800380B0`, called after movement (`0x800384BC`) | — |
| ModdedPos refreshed at the head of physics | CONFIRMED but incomplete | FACT `0x800383C0`. Also written by the movement code (`0x80037828..0x80037EF4`, next to the `+0x114` writes). H3 rows show `modded = pos − 10` on every tick. | "Pos+Mod on the fly" is right at hit-pass time. |
| Balance selection and threshold | CONFIRMED | FACT `0x80044550` remaps 0x1F..0x7F to 0x1E; `0x8004458C` loop; `0x80044524 sw 0x3df0`. Only caller is `0x8002C3A0` (`lbu 0xB` of the header). | `0x80044520` also zeroes the three equipment pointers `0x80146E00` (`0x80044538`). |
| Equipment entry `n+0x1E` (no remap) | CONFIRMED | FACT `0x800445E8 addiu 0x3e34`, base −0x3C; errors if n ≥ 0x62. balance.json: items 1-4 give 1/2/4/8, item 8 gives 50, 0x33/0x34 give 3, 0x11-0x14 have 0 attacks. | — |
| `ResolveBalanceTarget`: equipment term, r rules, minimum 1 | CONFIRMED | FACT `0x800446A8-0x80044778`. Index `g=[0x80146DF8]`: `g+1<count ? Attacks[g+1] : Attacks[0]`. | — |
| `ComputeNewHp`: 37 callers; RBT "called from `0x8003A528`" | CONFIRMED / REFUTED (site) | FACT 37 jal sites match B's list. The only RBT jal is at **`0x8003A3C0`**; `0x8003A528` is the HP store. | Fix the address. |
| Hero as target "reads response from `+0x1C4` (record 0, all zeros)" | CONFIRMED for the hit pass, REFUTED for damage | FACT `0x8003A3A0-B4`: `ComputeNewHp` uses sheet **`0x80127008`** when the target is the hero. That sheet is written by `0x800307E8` (`0x800309B8`, `0x80030A64`, …). The oracle measured it as all 16, giving 1 damage in H3. | Hero defence comes from `0x80127008` (c2), not `+0x1C4`. |
| Attacker attack read at the call | CONFIRMED | FACT `0x8003A39C lw 0x224`; `0x8003A3BC lw 0x1c8(v1)`, read at N+1, not at hit time | Pin this in c1/E14. |
| Spark routine | CONFIRMED | FACT `0x80039188`: 3× `jal 0x8003BDD8(0,9,0,cx,cy,cz)`, draws only when `t3≠0`, `multu 0x20001`, `hi−0x10000`. Centre computed at `0x80039684-0x80039744`. | — |
| `0x86`: SET `+0x1CC`, size 4 | CONFIRMED | FACT table `0x800991C4=0x8003FFD4`; `0x8004002C sw s0,0x1cc` | — |
| `0x87` predicate, size 3 | CONFIRMED | FACT `0x800991C8=0x80040048`; `+0x224`, `+0x1D4`, `+0x1C8`, attack byte ≠0, `&0xF == table[0x80098FA4]` (bytes 01 02 03 04 05 05 06 07) | — |
| `0x95` geometry and filters, slots 0..N inclusive, size 10 | CONFIRMED | FACT `0x80099200=0x800407C0`; x·24, y·16, z·16 (<<16); X/Z size wx·24 shared, Y size hy·16; mask zero gives 0; reads stride `0x294` slots, not the list | — |
| `0x71` sets Result = own `HitCounter` | CONFIRMED | FACT `0x80099170=0x8003FA10 lw 0x220(a0)` | — |
| `+0x1CC` and `+0x1D0` reader lists | CONFIRMED | Displacement scan: `+0x1D0` read only at `0x8003941C`. The `sh` at `0x80097924` is not identified (another struct, INFERENCE). | — |
| Opcode site census (76/182/43; maps 115/44/362) | UNVERIFIABLE (not re-derived) | Totals equal plan 0.2.5 | — |
| DLL facts (hook, counters pass, ModdedPos only written at spawn, fields commented out, AnimFlags written only in SyncAnimation) | CONFIRMED (spot checks) | `AlundraWorldProxy.cs:2625-2733`, `AlundraEntitySpawnFactory.cs:686-688`, `AlundraEntityScriptProxy.cs:407-427`, `AlundraFrameSyncPasses.cs:139,146` | — |

## (2) Report A: oracle

| Claim | Verdict | Evidence | Correction |
|---|---|---|---|
| D1 re-validated in the copy | CONFIRMED | `out/d1/comparison.txt`: 216/216, 0 differences, OVERALL PASS. Reproduced by my re-run. | — |
| Deterministic, all checks pass | CONFIRMED | Re-run exit 0, manifest identical | — |
| Target built from real records | CONFIRMED | `c1world.spawn_targets`: `map_14.json` `SpriteRecords[79]` laid out by `bank.py`, real `0x8003A1B8(0,i,0)`, balance 335 | Banks are rebuilt from the decoded JSON, not from disc bytes (the notes' own §7.6). |
| Target really inert | CONFIRMED | 201 handler addresses plus `0x8004205C` hooked. My scan: **none of the 201 is a `jal` target anywhere in the EXE**, so the hooks cannot stub shared code. | Program A is skipped (status forced to 2); the real A could set flags or animation (UNKNOWN). |
| No expected value embedded | CONFIRMED | Scenario specs carry only inputs (dx, pad). Numbers appear only in the mutation predicates, as intended. | `hit_model` reads geometry from emulated RAM rows. It is independent for the **rule**, not for box values. It is pairwise, so "first attacker wins" between two attackers is not modelled or exercised. `real_hits` needs `TouchingEntity`, so it would miss hits on immune targets (none in the corpus). |
| Swing boxes and ticks 1-4 / 5-6 / 7-11 / 12-21 | CONFIRMED | `data-boxes.txt` frame lengths 4,2,2,3,4,6. The export `Alundra/bankalundra_0_anim16_right.anim2d` keyframes at logical ticks 0/4/6/11 give (0,0) 29, (21,−8) 29, (22,−16) 23, (−5,−18) 23. Oracle tick t = logical t−1, which matches the D6 rule. | — |
| Reach [−9,59], first tick 1 for ≤38, Y windows, body [−18,20], H3 tick 17 | CONFIRMED (hand-derived) | Overlap rule: `hb≥tm: hb−tm<tsize`, else `tm−hb<bsize`. dx 59 → 28<29, 60 → 29 fails. Y at dx 40: [−13,14] ∪ [−5,22]. H3: 242−222.22=19.78<21 at tick 17, 22.2 at tick 16. | — |
| RNG `0x24D2C78A → 0x5AAD3C51` (9 draws), `0xA24755FC` (18) | CONFIRMED | Recomputed | — |
| H4a 5 → 30, damage 2 with the sword | CONFIRMED | Counters pass runs before the hit pass (`0x8003B3B0 < 3F0`): 25 at tick 5 reaches 0 at tick 30. `((1+1)·16)>>4`. | — |
| Incident: `rm -rf /d/tmp` | — | Irreversible, and the prior content is unknown | Main session should tell the author. |

## (3) What is still missing before writing the E20.c1 plan

1. **0x80038E84 placement.** Decide whether the DLL needs a no-op slot between physics and the hit pass, and whether it advances the RNG or touches the hero (`+0x1B8..+0x1BB`, per `critique.md` C12). This affects spark parity.
2. **Switch-tick read in the DLL.** The (animation, logical tick) cursor and the `CurrentAttack`/AnimFlags snapshot in `StepAnimationClock`, including the spawn animation. Write it as a pin against the oracle: hit on the press tick (H1-near) and AnimFlags `0x40` effective in the switch tick.
3. **Which list the clock and physics iterate.** The binary uses the post-program hit list; the DLL uses `_tickEntities`. Either c1 changes this or a deviation is recorded.
4. **Slot reuse order.** The binary reuses the first free slot; the DLL appends. An ADR is needed, or a pin limited to the non-recycled case.
5. **Balance loading.** Table, host seam, `D` parsed from the string, the `ApplySpawnInitialization` early-return case, hero record 0 vs sheet `0x80127008`.
6. **Immune path.** `0x80` gives no `TouchingEntity` but still sparks, `+0x1D0` and `HitCounter`. No oracle case covers it. Add a lever scenario with a nibble-8+ attacker, or a synthetic response-128 target, plus a two-attacker "first wins" scenario.
7. **Effect anchors and depth key.** `IAlundraEffectAnchor` has no implementation and the `+0x1BC` formula is unknown. Only needed for effects 4/5, which the basic sword never triggers.
8. **Equipment records.** In the binary they are filled by `UpdateItemEffectState`, which D-E20-14 assigns to c2. c1 needs a minimal, documented filler (weapon plus ring via `n+0x1E`) or an explicit deferral. Without it, damage is 1 instead of 2.
9. **No production consumer of `ComputeNewHp` in c1.** Beannoid D is native (E14.a). Only bytecode D (levers) shows a reaction. The plan must say "sparks only" for visible behaviour.
10. **Opcode census.** Re-derive or cite it, and decide on the `0x95` map-226 indices 8-11 (bytes after the table).
11. **Oracle gaps.** Other directions or weapons, a moving target, multiple attackers, and real lever records (115/116/117/44) in the harness.

## Proposed split

- **E20.c1a — the pass.** Balance table and threshold, `+0x1C4`/Hp/HpMax at spawn (entities and hero), `CurrentAttack` and frame boxes at the switch tick (D6 cursor), `HitBox`/ModdedPos at hit time, the hit list (status, `+0x20`, hero first), the real `UpdateBalanceRecords` including the immune path, `TouchingEntity`/`+0x1D0`/`HitCounter`, sparks (effect 9, centre, three RNG draws), and the `0x80038E84` slot decision. Acceptance: pins from this oracle (H1, H1-near, H1-both, H2/edge, H4a/b, H5, a reach subset), the N/N+1/clear test with two ticks per frame, and first-attacker-wins.
- **E20.c1b — consequences.** `ComputeNewHp`/`ResolveBalanceTarget` as pure functions (D2 oracle), equipment records (minimal filler or c2 hand-off), opcodes `0x86`/`0x87`/`0x95`/`0x71`, lever and bascule arcs (115/116/117/44), the `0x87` predicate on the 362 lid with a synthetic nibble-4 attacker, and effects 4/5 with the anchor and depth key. If the depth key stays UNKNOWN, move effects 4/5 to E20.f, where the first nibble 6/7/9/10 attacker appears.

c1a is visible on its own: sparks on the Beannoid of map 14. c1b unlocks E14.a and the lever arcs.
