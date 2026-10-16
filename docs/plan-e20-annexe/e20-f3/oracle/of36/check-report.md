**Verdict.** The binary frees the hit effects at hit+41 (effect 4) and hit+96 (effect 5), the same as the DLL. The "one tick early" gap does not exist, and no DLL change is needed. The f3b executor's inference (hit+42 and hit+97) does not hold against the binary. I confirm the probe's report.

## Evidence

**Decode of ALUN_CD.EXE (France), re-read by me**
- **FACT:** The frame function 0x8002E058 calls three things in a row: 0x8003C67C at 0x8002E100, the world tick 0x8003B388 at 0x8002E108, and the slot pass 0x8003C410 at 0x8002E110. The main loop calls it from 0x8002BC4C only.
- **FACT:** 0x8003B388 runs the passes in this order: 0x80038634, 0x800386D0, 0x80038998, 0x800384F4, 0x80038E18, 0x80038364, 0x80038E84, 0x80039300. Then 0x800399B8 runs, and it never touches the slot array.
- **FACT:** An EXE-wide scan finds one caller each for 0x8003C410 (0x8002E110), the animation step 0x8003BBDC (0x8003C480) and the position step 0x8003C284 (0x8003C488). So nothing else steps an effect slot.
- **FACT:** 0x80038E84 is not the effect stepper. It loops over the entity table (stride 0x294), keeps one mode-3 slot per entity through 0x8003BFE8, and spawns ground effects (ids 6, 0x15, …) through 0x8003BDD8. It never calls 0x8003BBDC or 0x8003C284. This is where the f3b executor went wrong.
- **FACT:** The hit pass creates effect 4 or 5 with `jal 0x8003BE74` at 0x80039624 and 0x80039660, but only when (response & 0xC0) != 0x80. The only other effect creator inside the hit pass is the sparks routine 0x80039188 (effect 9, called at 0x80039740).
- **FACT:** 0x8003BE74 takes the first free slot of 128 and calls 0x8003BB14. That sets mode 1, anchor = target, status +0x68 = 2, depth offset 1, and the complement bytes +0x6F and +0x71 = 0xFF so the first step always loads.
- **FACT:** The slot pass 0x8003C410 handles each status-2 slot like this:
  - If `[0x800DC4B8] & 0x48` is set, it skips the step and still draws the slot.
  - Otherwise, if +0x7D (done) is set, it sets status to 0 and does not draw the slot.
  - Otherwise it runs 0x8003BBDC then 0x8003C284, and appends the slot to the display list.
- **FACT:** Frame headers `0x80|ticks` give counter = ticks. A header of 0 gives counter 0xFF and done = 1. So the slot is drawn on d = 0..S and freed on d = S+1 (d = tick − T).
- **FACT:** The position step 0x8003C284 never writes status +0x68. An anchor in state 0 or 4 only turns mode 1 into mode 2.

**ORACLE: independent machine run (my own harness, `chk_bin.py`)**
- It runs the real hit pass, creators, 0x80038E84, slot pass and steps, in the frame-function order. I removed the stubs of 0x8003BDD8, 0x8003BE74 and 0x80038E84.
- Cases: nibbles 6, 10, 7 and 9, both setups (hero hits Beannoid, Beannoid hits hero as in test w1), T = 5, 8 and 13.
- For every case, slot read at the end of each tick:

| Effect | Active d | First absent | Drawn d | Done flag d |
|---|---|---|---|---|
| 4 (nibbles 6, 10) | 0..40 | d = 41 | 0..40 | 40 |
| 5 (nibbles 7, 9) | 0..95 | d = 96 | 0..95 | 95 |

- Sparks (effect 9, S = 25): all three sparks are first absent at d = 26.
- At birth, in the hit pass: mode 1, anchor = target, status 2, depth offset 1.
- Controls: nibble 1 creates no effect slot (only sparks). Nibble 6 with response 0x80 registers the hit (hit counter = 1) and creates sparks but no effect slot.
- Counterfactual: moving the slot pass before the world tick gives first absent d = 42 and 97, with drawing starting at d = 1. This shows the harness can tell the two orders apart, and it is the executor's reading, not the binary's order.

**Reading the DLL**
- **FACT:** In `AlundraWorldProxy.RunEntityBlock`, the entity block calls `BeginSource(tick, 1)` and ends with `RunActiveEffectsPass()`. That method is empty on purpose and stands for 0x80038E84 (E20.e, ground and shadow effects). The hit-pass hook then calls `AttachHitEffect`, which calls `_effectStage.Emit(CreateHitAttachedEffect)` marked (tick, rank 1).
- **FACT:** `AlundraEffectStage.RunFrame` applies every queued operation with `Tick <= k` and then calls `_reservoir.Tick(frozen, k)`. It runs after the whole entity loop, which mirrors 0x8002E110. So the effect is created and first updated in the same tick T, like the binary.
- **FACT:** In `AlundraEffectReservoir`, `CreateCore` sets the complement bytes. `Animate` loads frame 0 with counter = delay on the first update, then decrements. A `Destroy` end sets `Counter = 0xFF` and `Destroying`, and the next `Tick` frees the slot. The class doc states "drawn on ticks 0 to S, freed on S+1", and `AlundraEffectReservoirTests` line 306 pins it.
- **FACT:** The (w1) test reads the reservoir after each frame. It asserts `FirstSeenTick == HitTick` and `FirstAbsentTick == HitTick + 40 + 1` (effect 4) or `+ 95 + 1` (effect 5).
- **INFERENCE** (my own transcription of Animate, Tick and RunFrame, compared tick by tick with the binary; the C# was not run): zero differences over 118 ticks, in 8 runs (4 nibbles × 2 setups). Active state, counter +0x7C, done flag and frame ordinal all match.

## Is the DLL off by one?
- No, in either direction. The release is on the same tick as the binary's, the first frame appears on the same tick, and the spark release (T+26) matches too.
- The same alignment applies to everything the hit pass creates: effects 4 and 5, and the three sparks (effect 9). All use the same Emit at rank 1.
- With 2 or 3 ticks per frame, the first absent end-of-frame sample is `ceil((T+S+1)/N)*N`. That is only the granularity of end-of-frame sampling. At tick level the DLL state is identical.
- One known deviation, not a lifetime one: in a multi-tick frame the target's position is read at RunFrame, after all ticks. That is the documented gap and has no visible consequence.

## Proposed fix and blast radius
- **DLL:** no change. Moving the creation to T+1 or setting `FirstUpdateTick = tick + 1` would break the binary alignment shown by the oracle, and would shift every test that pins S+1. Those are (w1), the spark and f3b tests, and `AlundraEffectReservoirTests`. I did not run any tests (rules), so that list is inferred from the code.
- **Docs only**, for a docs executor in `docs/plan-e20-heros.md` (line numbers may have shifted):
  - The T3 result sentence around lines 5123–5125, which says "écart d'un tick avec le binaire … touche + 42".
  - The "Vérification" P3 item, lines 5132–5134.
  - The O-F3-6 row, line 5283: close it as "exact, no gap, settled on the binary 2026-10-16", citing the 0x8002E058 order.
  - Optional: a one-line comment on `RunActiveEffectsPass` saying the reservoir update corresponds to 0x8003C410, which runs after the whole world tick.

## Limits
- I ran the C# neither as tests nor as a build, as required, so the DLL side is code reading plus my Python transcription.
- The attack is injected before the hit pass, as in hitfx. Creation, hit pass, effects pass and slot pass are real.
- I did not run the real frame function 0x8002E058 or 0x8003C67C; that order is decoded statically.
- The pause case (`0x48`) was not exercised.
- The effect template at 0x8013C608 is assumed zero. A non-zero +0x7D would make every effect vanish before drawing, which the game contradicts.
- The mapping from effect index to table comes from the JSON. The raw DATAS.BIN tables run by the machine give S = 40 and 95, matching the task's 20×2 and 19×5.
- Housekeeping: I mistakenly created an empty `x/` directory at the repo root with a stray `mkdir`, and removed it with `rmdir` right after. Repo status is unchanged. The two submodule " m" entries were already there.

## Files
- Scratch folder: `C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/o-f3-6/checker/`
  - `chk_bin.py` is my independent machine harness. `chk_dll.py` is the DLL transcription compared with the binary. `scan_calls.py`, `bankinfo.py` and `hooks_list.py` are the static helpers. `chk_bin_summary.json` holds the run summary.
- Probe outputs consulted: `…/o-f3-6/agent/out/summary.json` and `dll_model_vs_binary.txt`.
- DLL files read: `Alundra/Scripts/AlundraEffectStage.cs`, `AlundraEffectReservoir.cs`, `AlundraWorldProxy.cs` (RunEntityBlock, RunActiveEffectsPass, AttachHitEffect, RunHitPassHook), `AlundraNativeEffects.cs`, and `Alundra.Tests/AlundraHitEffectWorldTests.cs`.