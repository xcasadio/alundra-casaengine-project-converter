**Report O-F3-6: the binary gives hit + 41 (effect 4) and hit + 96 (effect 5), the same as the DLL. The "one tick early" gap does not exist.**

The f3b executor's inference (hit + 42 and hit + 97) does not hold. It treated 0x80038E84 as the pass that steps effect slots. It is not.

## 1. Decode of the binary

- **FACT, frame function.** The frame function 0x8002E058 is called once per main-loop iteration (0x8002BC4C). In order it calls 0x8003C67C (0x8002E100), the world tick 0x8003B388 (0x8002E108), then the effect-slot pass 0x8003C410 (0x8002E110).
- **FACT, passes inside 0x8003B388.** They run in this order: 0x80038634, 0x800386D0, 0x80038998, 0x800384F4, 0x80038E18, 0x80038364, 0x80038E84, 0x80039300. After 0x80039300 comes 0x800399B8, which has no effect or slot access.
- **FACT, only caller.** 0x8003C410 is called only from 0x8002E110. It is the only caller of the animation step 0x8003BBDC (via 0x8003C480) and of the position step 0x8003C284. So an effect created in the hit pass of tick T is stepped by 0x8003C410 of the same tick T.
- **FACT, what 0x80038E84 is.** It loops over the entity table (stride 0x294, count [0x80134360]). For each entity it keeps one mode-3 slot through 0x8003BFE8 (status flipped between 1 and 2) and spawns ground effects through 0x8003BDD8. It never calls 0x8003BBDC or 0x8003C284. The DLL calls it RunActiveEffectsPass. INFERENCE: it is a terrain or entity effects pass.
- **FACT, creation, 0x8003BE74(0, id, 0, target, 1, 0, 0, 0).** It uses 0x8003B9C4 to find the first slot (of 128, from 0x80138608, stride 0x80) with status +0x68 equal to 0, and creates nothing if there is none. 0x8003BB14 then copies the 0x80-byte template over the slot and sets these fields:
  - +0x34 = 1 (attached mode) and +0x38 = target (the anchor).
  - +0x68 = 2 (active), +0x6C = 0 (global bank), +0x6E = id, +0x6F = ~id.
  - +0x70 = 0 (sequence target) and +0x71 = 0xFF (the complement, so the first step always loads).
  - +0x3C, +0x40, +0x44 = target +0x114, +0x118, +0x11C.
  - +0x60 = 1 (depth offset), and +0x48, +0x4C, +0x50 = 0.
  - Left from the template: +0x7C (counter) and +0x7D (done flag).
- **FACT, first step (0x8003BBDC).** Lookup 0x8003B9FC reads [0x80126EC0 + 0x1C] for the bank pointer. The step sets +0x7C and +0x7D to 0 and reads the first frame header, `0x80|ticks` plus a u16 image word. It then sets counter +0x7C = ticks and frame pointer +0xC = bank + 2*word + 2.
- **FACT, later steps.** Each later step decrements +0x7C and reads the next header at 0. Header byte 0x00 sets +0x7C = 0xFF and +0x7D = 1 (done). Byte 0x01 means loop.
- **FACT, slot pass 0x8003C410.** For each slot with status 2:
  - If `[0x800DC4B8] & 0x48` is set, it skips the step.
  - Otherwise, if +0x7D is set, it sets status to 0 and the slot is not drawn.
  - Otherwise it runs the step 0x8003BBDC, then 0x8003C284, then appends the slot to the display list.
  - So a slot is freed one slot pass after the step that set its done flag.
- **FACT, data.** The raw DATA/DATAS.BIN tables match map_alundra.json and effects-global.json. Effect 4 has 20 frames of 2 ticks (S = 40). Effect 5 has 19 frames of 5 ticks (S = 95). Both end with byte 0 (Destroy).

## 2. Machine run (ORACLE)

The run uses the real 0x8003BE74, 0x8003BDD8, 0x80038E84, 0x80039300 and 0x8003C410, in the frame order above. The attack is injected between 0x80038E84 and 0x80039300 of tick T = 8, as hitfx.py does.

| Nibble | Effect | Hero hits Beannoid | Beannoid hits hero (DLL test setup) |
|---|---|---|---|
| 6, 10 | 4 | slot created in hit pass at T | same |
| 7, 9 | 5 | slot created in hit pass at T | same |
| 1 (control) | none | no effect-4 or effect-5 slot | same |
| 6 with immune response 0x80 | none | no slot (3 sparks only) | same |

For each of nibbles 6, 7, 9 and 10 in both setups, the effect slot is attached (mode 1) to the target, with depth offset 1.

**Effect 4 (nibble 6), per tick d = tick - T:**
- Frame k is current at d = 2k and 2k+1. The counter reads 2 then 1.
- First step is at d = 0, in the same tick as the hit.
- Drawn on d = 0 to 40 (41 ticks).
- At d = 40 the slot pass sets counter = 255 and done = 1. The last frame (#19) is still drawn that tick.
- At d = 41 the slot pass sets status to 0, and the slot is not drawn.

**Effect 5 (nibble 7):**
- Frame k is current at d = 5k to 5k+4.
- Drawn on d = 0 to 95.
- Done flag at d = 95, freed at d = 96.

**Sparks (effect 9, S = 25):** freed at d = 26 in the binary.

## 3. Exact result in the (w1) convention

In test (w1), HitTick is the tick count at the end of whose frame `HitCounter >= 1`. The reservoir is read at the end of each frame. FirstSeen equals HitTick, and FirstAbsent is the first frame end with no slot of the effect.

- **ORACLE:** first tick without the effect = **T + 41** for effect 4 and **T + 96** for effect 5, with the slot read at the end of each tick. First seen = T.
- The test asserts exactly hit + 40 + 1 and hit + 95 + 1. The DLL matches the binary.

**Counterfactual.** The executor's reading (slot pass before the hit pass in a tick) gives T + 42 and T + 97 in my harness. That is not the binary's order.

## 4. Same frames at the same ticks?

- **INFERENCE** (code reading plus a Python transcription of AlundraEffectReservoir.Animate, Tick and AlundraEffectStage.RunFrame; the C# was not run): the DLL model matches the binary on every compared tick.
- The comparison covers active state, counter (+0x7C), done flag, frame ordinal and de-duplicated image-set index, over 111 ticks, for 6 runs (nibbles 6, 10, 7, 9, both setups). There are 0 differences.
- Reading the code, the creation is applied by RunFrame before that tick's reservoir update, so the first update is at tick T. This matches the binary.
- The slot with `Destroying` set is still drawn that tick and freed at the next one, like the binary's done flag.
- The DLL spark model also gives T + 26.

## 5. Limits

- The injected attack stands in for the earlier passes. The creation, the hit pass and the slot pass are real.
- The template 0x8013C608 is assumed zero. A static scan found no store into it, and the machine reports only reads at 0x8003BB34 to 0x8003BB44.
- 0x8003C67C (the map-record entity spawner) was not run. The `0x48` pause case was not exercised.
- The DLL test (w1) was not re-run; the plan's green result is cited as is.
- Another process changed `AlundraEntityScriptProxy.HitPass.cs` in the working tree during my run. I touched no repository file.

## 6. Consequence

**No DLL change is needed.** O-F3-6 can be closed as "no gap, exact". A docs executor should correct three places in docs/plan-e20-heros.md (line numbers may have shifted):
- The T3 result sentence that says "Écart d'un tick avec le binaire ... touche + 42".
- The "Vérification" P3 item (touche + 42 and + 97).
- The O-F3-6 row of the open-points table.

## Files

Scripts are in `C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/o-f3-6/agent/`:
- `fxlib.py` (harness: real stubs removed, effect bank laid out in RAM)
- `run_hit_effect.py` (main scenarios)
- `pass_trace.py` (state after each pass)
- `slot_dump.py` (slot words)
- `dll_model.py` (DLL transcription vs binary)
- `static_checks.py` (static facts, bank check, disassembly dumps)
- `callers.py`, `scan_tmpl.py`, `smoke.py`

Outputs are in the `out/` folder next to them:
- `hero_hits_bean_nib{6,10,7,9,1}_binary.txt`/`.json`, `bean_hits_hero_nib*_binary.*`, `*_nib6_immune_binary.*`, `*_slotpass_first.*` (counterfactual), and `summary.json`
- `pass_trace.txt`
- `slot_dump.txt`
- `dll_model_vs_binary.txt`
- `static_facts.txt`
- `bank_check.txt`
- `disasm/*.txt`