# The step search of the binary (`ComputeXYPosition` `0x80037730`) against the engine mover: read-only discovery (2026-10-24)

Read-only reading of `ALUN_CD.EXE` (France), of the engine (`CasaEngineMonogame/`, `CharacterControllerComponent`) and of the DLL, run while
E20.g4b was building in the same checkout (no build, no test). Asked by the main session after O-G3-3 (D-E20-77) and O-E20-24 seemed to share a root.

- `report-main.md`: the reader's report (S1 to S4), in French. `critic-main.md`: the fresh critic's report, in French.
- Verdicts: S1 CONFIRMED (the binary searches the step jointly on X and Y by halving; it returns the entity verdict of the second-to-last attempt;
  `ForceAdjusted` only when no sub-step was accepted; a model replayed against the real code: 0 mismatch on 55 000 vectors and 9 120 scenario ticks,
  449/449 instructions, 14 mutants killed); S2 CONFIRMED with omissions; S3: an exact port needs the integer XY truth in the DLL (the engine root is
  float32: beyond 256 px the first `ForceAdjusted` differs by a tick in 10 to 37 % of the phases, MEASURED), where the loop lives is the open question;
  the critic adds an option D (a contact-only shadow).
- `work/`, `out/`, `lst/`, `critic/`: scripts, outputs and listings (`work/xymodel.py`, `work/xyfuzz.py`, `work/xymut.py`).

Open at the time of writing: Q-STEP (docs/plan-e20-heros.md §2), for the author.
